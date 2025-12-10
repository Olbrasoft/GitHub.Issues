using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Olbrasoft.GitHub.Issues.Business;
using Olbrasoft.GitHub.Issues.Data.Commands.EventCommands;
using Olbrasoft.GitHub.Issues.Data.Entities;

namespace Olbrasoft.GitHub.Issues.Sync.Services;

/// <summary>
/// Service for synchronizing issue events from GitHub.
/// </summary>
public class EventSyncService : IEventSyncService
{
    private readonly IIssueSyncBusinessService _issueSyncBusiness;
    private readonly IEventSyncBusinessService _eventSyncBusiness;
    private readonly HttpClient _httpClient;
    private readonly SyncSettings _syncSettings;
    private readonly ILogger<EventSyncService> _logger;

    public EventSyncService(
        IIssueSyncBusinessService issueSyncBusiness,
        IEventSyncBusinessService eventSyncBusiness,
        HttpClient httpClient,
        IOptions<GitHubSettings> settings,
        IOptions<SyncSettings> syncSettings,
        ILogger<EventSyncService> logger)
    {
        _issueSyncBusiness = issueSyncBusiness;
        _eventSyncBusiness = eventSyncBusiness;
        _httpClient = httpClient;
        _syncSettings = syncSettings.Value;
        _logger = logger;

        // Configure HttpClient for GitHub API
        _httpClient.BaseAddress = new Uri("https://api.github.com/");
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        _httpClient.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
        _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Olbrasoft-GitHub-Issues-Sync", "1.0"));

        if (!string.IsNullOrEmpty(settings.Value.Token))
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", settings.Value.Token);
        }
    }

    public async Task SyncEventsAsync(
        Repository repository,
        string owner,
        string repo,
        DateTimeOffset? since = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Syncing issue events for {Owner}/{Repo} using bulk API ({Mode})",
            owner, repo, since.HasValue ? "incremental" : "full");

        // Cache event types for faster lookup
        var eventTypes = await _eventSyncBusiness.GetAllEventTypesAsync(cancellationToken);

        // Get all issues for this repository (to map issue numbers to IDs)
        var issuesByNumber = await _issueSyncBusiness.GetIssuesByRepositoryAsync(repository.Id, cancellationToken);

        // Get existing event IDs to avoid duplicates
        var existingEventIds = await _eventSyncBusiness.GetExistingEventIdsAsync(repository.Id, cancellationToken);

        var allEvents = new List<JsonElement>();
        var page = 1;
        var stopEarly = false;

        // Fetch events using bulk repo events endpoint with pagination
        // GitHub returns events in descending order (newest first)
        // For incremental sync, stop when we hit events older than 'since'
        while (true)
        {
            var url = $"repos/{owner}/{repo}/issues/events?per_page={_syncSettings.GitHubApiPageSize}&page={page}";
            _logger.LogDebug("Fetching events page {Page}", page);

            var response = await _httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);

            var pageEvents = doc.RootElement.EnumerateArray().ToList();
            if (pageEvents.Count == 0)
            {
                break;
            }

            // Clone elements and check for early termination
            foreach (var evt in pageEvents)
            {
                // Check if event is older than 'since' timestamp - if so, stop
                if (since.HasValue)
                {
                    var createdAt = evt.GetProperty("created_at").GetDateTimeOffset();
                    if (createdAt < since.Value)
                    {
                        stopEarly = true;
                        _logger.LogDebug("Stopping events fetch - hit event from {CreatedAt} (before {Since})", createdAt, since.Value);
                        break;
                    }
                }
                allEvents.Add(evt.Clone());
            }

            _logger.LogDebug("Fetched {Count} events on page {Page}", pageEvents.Count, page);

            if (stopEarly || pageEvents.Count < _syncSettings.GitHubApiPageSize)
            {
                break;
            }

            page++;
        }

        _logger.LogInformation("Found {Count} events to process for {Owner}/{Repo}", allEvents.Count, owner, repo);

        var eventsToSave = new List<IssueEventData>();
        var skippedCount = 0;

        foreach (var ghEvent in allEvents)
        {
            // Get GitHub event ID
            var eventId = ghEvent.GetProperty("id").GetInt64();

            // Skip if already synced
            if (existingEventIds.Contains(eventId))
            {
                continue;
            }

            // Get issue number from the issue object in the event
            if (!ghEvent.TryGetProperty("issue", out var issueElement))
            {
                skippedCount++;
                continue;
            }

            var issueNumber = issueElement.GetProperty("number").GetInt32();

            // Find the issue in our database
            if (!issuesByNumber.TryGetValue(issueNumber, out var issue))
            {
                skippedCount++;
                continue;
            }

            // Get event type
            var eventTypeName = ghEvent.GetProperty("event").GetString() ?? "";

            if (!eventTypes.TryGetValue(eventTypeName, out var eventType))
            {
                _logger.LogDebug("Unknown event type: {EventType}, skipping", eventTypeName);
                skippedCount++;
                continue;
            }

            // Get actor info
            int? actorId = null;
            string? actorLogin = null;
            if (ghEvent.TryGetProperty("actor", out var actorElement) && actorElement.ValueKind != JsonValueKind.Null)
            {
                if (actorElement.TryGetProperty("id", out var actorIdElement))
                {
                    actorId = actorIdElement.GetInt32();
                }
                if (actorElement.TryGetProperty("login", out var actorLoginElement))
                {
                    actorLogin = actorLoginElement.GetString();
                }
            }

            // Get created_at
            var createdAt = ghEvent.GetProperty("created_at").GetDateTimeOffset();

            eventsToSave.Add(new IssueEventData(
                issue.Id,
                eventType.Id,
                eventId,
                createdAt,
                actorId,
                actorLogin));

            existingEventIds.Add(eventId); // Track to avoid duplicates within same sync
        }

        if (eventsToSave.Count > 0)
        {
            var newEventsCount = await _eventSyncBusiness.SaveEventsBatchAsync(eventsToSave, existingEventIds, cancellationToken);
            _logger.LogInformation("Synced {Count} new issue events (skipped {Skipped})", newEventsCount, skippedCount);
        }
        else
        {
            _logger.LogInformation("No new issue events to sync (skipped {Skipped})", skippedCount);
        }
    }
}
