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
