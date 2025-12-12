using Olbrasoft.GitHub.Issues.Data.Dtos;

namespace Olbrasoft.GitHub.Issues.Business.Services;

/// <summary>
/// Service for generating issue summaries with translation.
/// Single responsibility: Orchestrate summarization and translation, send notifications.
/// </summary>
public interface IIssueSummaryService
{
    /// <summary>
    /// Generates AI summary from body text and sends notification via SignalR.
    /// </summary>
    /// <param name="issueId">Database issue ID</param>
    /// <param name="body">Issue body text to summarize</param>
    /// <param name="language">Language preference: "en" (English only), "cs" (Czech only), "both" (English first, then Czech)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task GenerateSummaryAsync(int issueId, string body, string language, CancellationToken cancellationToken = default);
}
