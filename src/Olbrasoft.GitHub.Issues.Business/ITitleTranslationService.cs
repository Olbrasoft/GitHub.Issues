namespace Olbrasoft.GitHub.Issues.Business;

/// <summary>
/// Service for translating issue titles to Czech and notifying via SignalR.
/// </summary>
public interface ITitleTranslationService
{
    /// <summary>
    /// Translates an issue title to Czech and sends notification via SignalR.
    /// If translation is cached, sends it immediately without calling AI.
    /// </summary>
    /// <param name="issueId">Database ID of the issue.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task TranslateTitleAsync(int issueId, CancellationToken cancellationToken = default);
}
