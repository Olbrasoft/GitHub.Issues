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
        }
        _logger.LogDebug("Client {ConnectionId} subscribed to {Count} issues", Context.ConnectionId, issueIds.Length);
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
        _logger.LogDebug("Client {ConnectionId} unsubscribed from {Count} issues", Context.ConnectionId, issueIds.Length);
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogDebug("Client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogDebug("Client disconnected: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}
