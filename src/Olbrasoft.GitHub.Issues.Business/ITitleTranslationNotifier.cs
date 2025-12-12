namespace Olbrasoft.GitHub.Issues.Business;

/// <summary>
/// DTO for title translation notification sent via SignalR.
/// </summary>
/// <param name="IssueId">Database ID of the issue.</param>
/// <param name="TranslatedTitle">The translated title text.</param>
/// <param name="Language">Target language code (en, de, cs).</param>
/// <param name="Provider">Translation provider (e.g., "DeepL", "Azure").</param>
public record TitleTranslationNotificationDto(
    int IssueId,
    string TranslatedTitle,
    string Language,
    string Provider);

/// <summary>
/// Interface for notifying clients when title translation is ready.
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
