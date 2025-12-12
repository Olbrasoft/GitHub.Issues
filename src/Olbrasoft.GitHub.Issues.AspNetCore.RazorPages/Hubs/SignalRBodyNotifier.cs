using Microsoft.AspNetCore.SignalR;
using Olbrasoft.GitHub.Issues.Business;

namespace Olbrasoft.GitHub.Issues.AspNetCore.RazorPages.Hubs;

/// <summary>
/// SignalR implementation of IBodyNotifier.
/// Sends issue body preview to subscribed clients when fetched from GitHub.
/// </summary>
public class SignalRBodyNotifier : IBodyNotifier
{
    private readonly IHubContext<IssueUpdatesHub> _hubContext;
    private readonly ILogger<SignalRBodyNotifier> _logger;

    public SignalRBodyNotifier(
        IHubContext<IssueUpdatesHub> hubContext,
        ILogger<SignalRBodyNotifier> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task NotifyBodyReceivedAsync(BodyNotificationDto notification, CancellationToken cancellationToken = default)
    {
        var groupName = $"issue-{notification.IssueId}";

        _logger.LogDebug(
            "[SignalR] Broadcasting BodyReceived to group {Group} for issue {Id}",
            groupName, notification.IssueId);

        await _hubContext.Clients.Group(groupName).SendAsync(
            "BodyReceived",
            new
            {
                issueId = notification.IssueId,
                bodyPreview = notification.BodyPreview
            },
            cancellationToken);
    }
}
