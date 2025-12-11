namespace Olbrasoft.GitHub.Issues.Business;

/// <summary>
/// Result of title translation operation.
/// </summary>
public record TitleTranslationResult(
    int IssueId,
    string? CzechTitle,
    bool Success,
    string? Error = null);

/// <summary>
/// Service for translating issue titles to Czech.
/// </summary>
public interface ITitleTranslationService
{
    /// <summary>
    /// Translates a single issue title to Czech.
    /// Returns cached translation if available and valid.
    /// </summary>
    Task<TitleTranslationResult> TranslateTitleAsync(int issueId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Translates multiple issue titles to Czech.
    /// Skips issues that already have valid cached translations.
    /// </summary>
    Task<IReadOnlyList<TitleTranslationResult>> TranslateTitlesAsync(
        IReadOnlyList<int> issueIds,
        CancellationToken cancellationToken = default);
}
