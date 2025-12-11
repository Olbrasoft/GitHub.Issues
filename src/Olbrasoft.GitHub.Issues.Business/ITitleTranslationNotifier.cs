namespace Olbrasoft.GitHub.Issues.Business;

/// <summary>
/// DTO for title translation notification sent via SignalR.
/// </summary>
public record TitleTranslationNotificationDto(
    int IssueId,
    string CzechTitle,
    string Provider);

/// <summary>
/// Interface for notifying clients when Czech title translation is ready.
/// </summary>
public interface ITitleTranslationNotifier
{
    /// <summary>
    /// Sends translated title to subscribed clients via SignalR.
    /// </summary>
    Task NotifyTitleTranslatedAsync(TitleTranslationNotificationDto notification, CancellationToken cancellationToken = default);
}

/// <summary>
/// Null implementation for when SignalR is not configured.
/// </summary>
public class NullTitleTranslationNotifier : ITitleTranslationNotifier
{
    public Task NotifyTitleTranslatedAsync(TitleTranslationNotificationDto notification, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
