using Microsoft.AspNetCore.SignalR;
using Olbrasoft.GitHub.Issues.Business;

namespace Olbrasoft.GitHub.Issues.AspNetCore.RazorPages.Hubs;

/// <summary>
/// SignalR implementation of ITranslationNotifier.
/// Sends title translations to subscribed clients when ready.
/// </summary>
public class SignalRTranslationNotifier : ITranslationNotifier
{
    private readonly IHubContext<IssueUpdatesHub> _hubContext;
    private readonly ILogger<SignalRTranslationNotifier> _logger;

    public SignalRTranslationNotifier(
        IHubContext<IssueUpdatesHub> hubContext,
        ILogger<SignalRTranslationNotifier> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task NotifyTitleTranslationAsync(
        TitleTranslationNotificationDto notification,
        CancellationToken cancellationToken = default)
    {
        var groupName = $"issue-{notification.IssueId}";

        await _hubContext.Clients.Group(groupName).SendAsync(
            "TitleTranslationReceived",
            new
            {
                IssueId = notification.IssueId,
                CzechTitle = notification.CzechTitle
            },
            cancellationToken);

        _logger.LogDebug(
            "Broadcast title translation for issue {Id} to group {Group}",
            notification.IssueId, groupName);
    }
}
