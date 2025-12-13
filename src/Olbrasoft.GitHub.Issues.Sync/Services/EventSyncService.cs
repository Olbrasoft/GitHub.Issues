using Microsoft.Extensions.Logging;
using Olbrasoft.GitHub.Issues.Business;
using Olbrasoft.GitHub.Issues.Data.Commands.EventCommands;
using Olbrasoft.GitHub.Issues.Data.Entities;
using Olbrasoft.GitHub.Issues.Sync.ApiClients;

namespace Olbrasoft.GitHub.Issues.Sync.Services;

/// <summary>
/// Orchestrates issue event synchronization from GitHub.
/// Single responsibility: Coordinate sync workflow between API client and business services.
/// </summary>
public class EventSyncService : IEventSyncService
{
    private readonly IGitHubEventApiClient _apiClient;
    private readonly IIssueSyncBusinessService _issueSyncBusiness;
    private readonly IEventSyncBusinessService _eventSyncBusiness;
    private readonly ILogger<EventSyncService> _logger;

    public EventSyncService(
        IGitHubEventApiClient apiClient,
        IIssueSyncBusinessService issueSyncBusiness,
        IEventSyncBusinessService eventSyncBusiness,
        ILogger<EventSyncService> logger)
    {
        _apiClient = apiClient;
        _issueSyncBusiness = issueSyncBusiness;
        _eventSyncBusiness = eventSyncBusiness;
        _logger = logger;
    }

    public async Task SyncEventsAsync(
        Repository repository,
        string owner,
        string repo,
        DateTimeOffset? since = null,
        CancellationToken cancellationToken = default)
    {
        // Fetch events from GitHub API
        var allEvents = await _apiClient.FetchEventsAsync(owner, repo, since, cancellationToken);

        // Cache event types and issues for mapping
        var eventTypes = await _eventSyncBusiness.GetAllEventTypesAsync(cancellationToken);
        var issuesByNumber = await _issueSyncBusiness.GetIssuesByRepositoryAsync(repository.Id, cancellationToken);
        var existingEventIds = await _eventSyncBusiness.GetExistingEventIdsAsync(repository.Id, cancellationToken);

        var eventsToSave = new List<IssueEventData>();
        var skippedCount = 0;

        foreach (var evt in allEvents)
        {
            // Skip if already synced
            if (existingEventIds.Contains(evt.GitHubEventId))
            {
                continue;
            }

            // Find the issue in our database
            if (!issuesByNumber.TryGetValue(evt.IssueNumber, out var issue))
            {
                skippedCount++;
                continue;
            }

            // Map event type
            if (!eventTypes.TryGetValue(evt.EventType, out var eventType))
            {
                _logger.LogDebug("Unknown event type: {EventType}, skipping", evt.EventType);
                skippedCount++;
                continue;
            }

            eventsToSave.Add(new IssueEventData(
                issue.Id,
                eventType.Id,
                evt.GitHubEventId,
                evt.CreatedAt));

            existingEventIds.Add(evt.GitHubEventId); // Track to avoid duplicates
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
