using Microsoft.AspNetCore.SignalR;
using Olbrasoft.GitHub.Issues.Sync.Webhooks;

namespace Olbrasoft.GitHub.Issues.AspNetCore.RazorPages.Hubs;

/// <summary>
/// SignalR implementation of IIssueUpdateNotifier.
/// Broadcasts issue updates to subscribed clients.
/// </summary>
public class SignalRIssueUpdateNotifier : IIssueUpdateNotifier
{
    private readonly IHubContext<IssueUpdatesHub> _hubContext;
    private readonly ILogger<SignalRIssueUpdateNotifier> _logger;

    public SignalRIssueUpdateNotifier(
        IHubContext<IssueUpdatesHub> hubContext,
        ILogger<SignalRIssueUpdateNotifier> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task NotifyIssueUpdatedAsync(IssueUpdateDto update, CancellationToken cancellationToken = default)
    {
        var groupName = $"issue-{update.IssueId}";

        await _hubContext.Clients.Group(groupName).SendAsync(
            "IssueUpdated",
            new
            {
                IssueId = update.IssueId,
                GitHubNumber = update.GitHubNumber,
                IsOpen = update.IsOpen,
                Title = update.Title,
                Labels = update.Labels.Select(l => new { l.Name, l.Color })
            },
            cancellationToken);

        _logger.LogDebug(
            "Broadcast update for issue #{Number} (ID: {Id}) to group {Group}",
            update.GitHubNumber, update.IssueId, groupName);
    }
}
