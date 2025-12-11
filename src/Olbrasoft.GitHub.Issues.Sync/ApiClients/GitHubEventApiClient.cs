using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Olbrasoft.GitHub.Issues.Sync.Services;

namespace Olbrasoft.GitHub.Issues.Sync.ApiClients;

/// <summary>
/// GitHub REST API client for fetching issue events.
/// Single responsibility: HTTP communication and JSON parsing for events.
/// </summary>
public class GitHubEventApiClient : IGitHubEventApiClient
{
    private readonly HttpClient _httpClient;
    private readonly int _pageSize;
    private readonly ILogger<GitHubEventApiClient> _logger;

    public GitHubEventApiClient(
        HttpClient httpClient,
        IOptions<SyncSettings> syncSettings,
        ILogger<GitHubEventApiClient> logger)
    {
        _httpClient = httpClient;
        _pageSize = syncSettings.Value.GitHubApiPageSize;
        _logger = logger;
    }

    public async Task<IReadOnlyList<GitHubEventDto>> FetchEventsAsync(
        string owner,
        string repo,
        DateTimeOffset? since = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Fetching events for {Owner}/{Repo} ({Mode})",
            owner, repo, since.HasValue ? "incremental" : "full");

        var allEvents = new List<GitHubEventDto>();
        var page = 1;
        var stopEarly = false;

        // GitHub returns events in descending order (newest first)
        while (true)
        {
            var url = $"repos/{owner}/{repo}/issues/events?per_page={_pageSize}&page={page}";
            _logger.LogDebug("Fetching events page {Page}", page);

            var response = await _httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);

            var pageEvents = ParseEventsFromJson(doc.RootElement, since, out stopEarly);
            allEvents.AddRange(pageEvents);

            _logger.LogDebug("Fetched {Count} events on page {Page}", pageEvents.Count, page);

            if (stopEarly || pageEvents.Count < _pageSize)
            {
                break;
            }

            page++;
        }

        _logger.LogInformation("Found {Count} events for {Owner}/{Repo}", allEvents.Count, owner, repo);
        return allEvents;
    }

    private List<GitHubEventDto> ParseEventsFromJson(JsonElement root, DateTimeOffset? since, out bool stopEarly)
    {
        var events = new List<GitHubEventDto>();
        stopEarly = false;

        foreach (var evt in root.EnumerateArray())
        {
            var createdAt = evt.GetProperty("created_at").GetDateTimeOffset();

            // For incremental sync, stop when we hit older events
            if (since.HasValue && createdAt < since.Value)
            {
                stopEarly = true;
                _logger.LogDebug("Stopping - hit event from {CreatedAt} (before {Since})", createdAt, since.Value);
                break;
            }

            // Skip events without issue
            if (!evt.TryGetProperty("issue", out var issueElement))
            {
                continue;
            }

            var eventId = evt.GetProperty("id").GetInt64();
            var issueNumber = issueElement.GetProperty("number").GetInt32();
            var eventType = evt.GetProperty("event").GetString() ?? "";

            // Extract actor info
            int? actorId = null;
            string? actorLogin = null;
            if (evt.TryGetProperty("actor", out var actorElement) && actorElement.ValueKind != JsonValueKind.Null)
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

            events.Add(new GitHubEventDto(eventId, issueNumber, eventType, createdAt, actorId, actorLogin));
        }

        return events;
    }
}
