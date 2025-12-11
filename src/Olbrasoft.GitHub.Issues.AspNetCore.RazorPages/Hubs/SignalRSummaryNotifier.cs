using Microsoft.AspNetCore.SignalR;
using Olbrasoft.GitHub.Issues.Business;

namespace Olbrasoft.GitHub.Issues.AspNetCore.RazorPages.Hubs;

/// <summary>
/// SignalR implementation of ISummaryNotifier.
/// Sends AI summary to subscribed clients when ready.
/// </summary>
public class SignalRSummaryNotifier : ISummaryNotifier
{
    private readonly IHubContext<IssueUpdatesHub> _hubContext;
    private readonly ILogger<SignalRSummaryNotifier> _logger;

    public SignalRSummaryNotifier(
        IHubContext<IssueUpdatesHub> hubContext,
        ILogger<SignalRSummaryNotifier> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task NotifySummaryReadyAsync(SummaryNotificationDto notification, CancellationToken cancellationToken = default)
    {
        var groupName = $"issue-{notification.IssueId}";

        await _hubContext.Clients.Group(groupName).SendAsync(
            "SummaryReceived",
            new
            {
                IssueId = notification.IssueId,
                Summary = notification.Summary,
                Provider = notification.Provider
            },
            cancellationToken);

        _logger.LogDebug(
            "Broadcast summary for issue {Id} to group {Group} (provider: {Provider})",
            notification.IssueId, groupName, notification.Provider);
    }
}
