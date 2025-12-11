using Microsoft.AspNetCore.SignalR;
using Olbrasoft.GitHub.Issues.Business;

namespace Olbrasoft.GitHub.Issues.AspNetCore.RazorPages.Hubs;

/// <summary>
/// SignalR implementation of ITitleTranslationNotifier.
/// Sends Czech title translation to subscribed clients when ready.
/// </summary>
public class SignalRTitleTranslationNotifier : ITitleTranslationNotifier
{
    private readonly IHubContext<IssueUpdatesHub> _hubContext;
    private readonly ILogger<SignalRTitleTranslationNotifier> _logger;

    public SignalRTitleTranslationNotifier(
        IHubContext<IssueUpdatesHub> hubContext,
        ILogger<SignalRTitleTranslationNotifier> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task NotifyTitleTranslatedAsync(TitleTranslationNotificationDto notification, CancellationToken cancellationToken = default)
    {
        var groupName = $"issue-{notification.IssueId}";

        _logger.LogInformation(
            "[SignalR] Broadcasting TitleTranslated to group {Group} for issue {Id}",
            groupName, notification.IssueId);

        await _hubContext.Clients.Group(groupName).SendAsync(
            "TitleTranslated",
            new
            {
                IssueId = notification.IssueId,
                CzechTitle = notification.CzechTitle,
                Provider = notification.Provider
            },
            cancellationToken);

        _logger.LogInformation(
            "[SignalR] TitleTranslated broadcast COMPLETE for issue {Id}",
            notification.IssueId);
    }
}
