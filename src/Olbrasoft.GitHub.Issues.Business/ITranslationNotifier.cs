namespace Olbrasoft.GitHub.Issues.Business;

/// <summary>
/// DTO for title translation notification sent via SignalR.
/// </summary>
public record TitleTranslationNotificationDto(
    int IssueId,
    string CzechTitle);

/// <summary>
/// Interface for notifying clients when title translation is ready.
/// </summary>
public interface ITranslationNotifier
{
    /// <summary>
    /// Sends title translation to subscribed clients via SignalR.
    /// </summary>
    Task NotifyTitleTranslationAsync(
        TitleTranslationNotificationDto notification,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Null implementation for when SignalR is not configured.
/// </summary>
public class NullTranslationNotifier : ITranslationNotifier
{
    public Task NotifyTitleTranslationAsync(
        TitleTranslationNotificationDto notification,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
