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

        _logger.LogInformation(
            "[SignalR] Broadcasting SummaryReceived to group {Group} for issue {Id}",
            groupName, notification.IssueId);

        await _hubContext.Clients.Group(groupName).SendAsync(
            "SummaryReceived",
            new
            {
                issueId = notification.IssueId,
                summary = notification.Summary,
                provider = notification.Provider,
                language = notification.Language
            },
            cancellationToken);

        _logger.LogInformation(
            "[SignalR] Broadcast COMPLETE for issue {Id} via {Provider}",
            notification.IssueId, notification.Provider);
    }
}
