using Microsoft.AspNetCore.SignalR;

namespace Olbrasoft.GitHub.Issues.AspNetCore.RazorPages.Hubs;

/// <summary>
/// SignalR hub for real-time issue updates.
/// Clients subscribe to specific issues they're viewing and receive updates when those issues change.
/// </summary>
public class IssueUpdatesHub : Hub
{
    private readonly ILogger<IssueUpdatesHub> _logger;

    public IssueUpdatesHub(ILogger<IssueUpdatesHub> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Client subscribes to updates for specific issues they're currently viewing.
    /// </summary>
    /// <param name="issueIds">Array of issue IDs (database IDs) to subscribe to</param>
    public async Task SubscribeToIssues(int[] issueIds)
    {
        foreach (var id in issueIds)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"issue-{id}");
            _logger.LogInformation("[Hub] Client {ConnectionId} joined group issue-{Id}", Context.ConnectionId, id);
        }
        _logger.LogInformation("[Hub] Client {ConnectionId} subscribed to {Count} issues: [{Ids}]",
            Context.ConnectionId, issueIds.Length, string.Join(", ", issueIds));
    }

    /// <summary>
    /// Client unsubscribes from issue updates (e.g., when navigating away).
    /// </summary>
    /// <param name="issueIds">Array of issue IDs to unsubscribe from</param>
    public async Task UnsubscribeFromIssues(int[] issueIds)
    {
        foreach (var id in issueIds)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"issue-{id}");
        }
        _logger.LogInformation("[Hub] Client {ConnectionId} unsubscribed from {Count} issues", Context.ConnectionId, issueIds.Length);
    }

    /// <summary>
    /// Client subscribes to search result updates.
    /// When new issues are added via webhook, subscribed clients will be notified.
    /// </summary>
    /// <param name="repositoryFullName">Optional: Filter by repository (e.g., "owner/repo"). Empty string for all repos.</param>
    public async Task SubscribeToSearchResults(string repositoryFullName = "")
    {
        // All search result subscribers join the main group
        await Groups.AddToGroupAsync(Context.ConnectionId, SignalRSearchResultNotifier.SearchResultsGroup);

        // If filtering by repository, also join the repo-specific group
        if (!string.IsNullOrWhiteSpace(repositoryFullName))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"search-{repositoryFullName}");
            _logger.LogInformation("[Hub] Client {ConnectionId} subscribed to search results for repo {Repo}",
                Context.ConnectionId, repositoryFullName);
        }
        else
        {
            _logger.LogInformation("[Hub] Client {ConnectionId} subscribed to all search results", Context.ConnectionId);
        }
    }

    /// <summary>
    /// Client unsubscribes from search result updates.
    /// </summary>
    /// <param name="repositoryFullName">Optional: The repository to unsubscribe from</param>
    public async Task UnsubscribeFromSearchResults(string repositoryFullName = "")
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, SignalRSearchResultNotifier.SearchResultsGroup);

        if (!string.IsNullOrWhiteSpace(repositoryFullName))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"search-{repositoryFullName}");
        }

        _logger.LogInformation("[Hub] Client {ConnectionId} unsubscribed from search results", Context.ConnectionId);
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("[Hub] Client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("[Hub] Client disconnected: {ConnectionId}, Error: {Error}",
            Context.ConnectionId, exception?.Message ?? "none");
        await base.OnDisconnectedAsync(exception);
    }
}
