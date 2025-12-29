namespace Olbrasoft.GitHub.Issues.Business.Summarization;

/// <summary>
/// Orchestrates AI summarization for GitHub issues.
/// Single responsibility: AI summarization orchestration only (SRP).
/// </summary>
public interface IIssueSummaryOrchestrator
{
    /// <summary>
    /// Generates AI summary for a single issue.
    /// Default behavior: generates both English and Czech summaries.
    /// </summary>
    /// <param name="issueId">The issue ID to generate summary for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task GenerateSummaryAsync(int issueId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates AI summary for a single issue with language preference.
    /// </summary>
    /// <param name="issueId">The issue ID to generate summary for.</param>
    /// <param name="language">Target language ("en", "cs", or "both").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task GenerateSummaryAsync(int issueId, string language, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates AI summaries for multiple issues with bodies.
    /// Triggers summarization sequentially to avoid LLM overload.
    /// </summary>
    /// <param name="issuesWithBodies">Collection of issue IDs with their body content.</param>
    /// <param name="language">Target language for summaries (default: "en").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task GenerateSummariesAsync(
        IEnumerable<(int IssueId, string Body)> issuesWithBodies,
        string language = "en",
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates AI summary from a pre-fetched body and sends notification via SignalR.
    /// </summary>
    /// <param name="issueId">The issue ID.</param>
    /// <param name="body">Pre-fetched issue body content.</param>
    /// <param name="language">Target language for summary.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task GenerateSummaryFromBodyAsync(int issueId, string body, string language, CancellationToken cancellationToken = default);
}
