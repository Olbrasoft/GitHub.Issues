namespace Olbrasoft.GitHub.Issues.Business;

/// <summary>
/// Service for translating issue titles and notifying via SignalR.
/// </summary>
public interface ITitleTranslationService
{
    /// <summary>
    /// Translates an issue title to the target language and sends notification via SignalR.
    /// </summary>
    /// <param name="issueId">Database ID of the issue.</param>
    /// <param name="targetLanguage">Target language code (en, de, cs). Defaults to "cs" (Czech).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task TranslateTitleAsync(int issueId, string targetLanguage = "cs", CancellationToken cancellationToken = default);
}
