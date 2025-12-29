namespace Olbrasoft.GitHub.Issues.Business.Summarization;

/// <summary>
/// Result of AI summarization operation.
/// </summary>
/// <param name="Success">True if summarization succeeded</param>
/// <param name="Summary">Generated summary text</param>
/// <param name="Provider">AI provider info (e.g., "Cerebras/llama3.1-8b")</param>
/// <param name="Error">Error message if failed</param>
public record AiSummarizationResult(
    bool Success,
    string? Summary,
    string? Provider,
    string? Error);

/// <summary>
/// Service for AI-powered summarization of GitHub issues.
/// Single responsibility: Generate summaries using AI (SRP).
/// </summary>
public interface IAiSummarizationService
{
    /// <summary>
    /// Generates AI summary for GitHub issue body.
    /// </summary>
    /// <param name="body">Issue body text to summarize</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Summarization result with success status, summary text, and provider info</returns>
    Task<AiSummarizationResult> GenerateSummaryAsync(
        string body,
        CancellationToken cancellationToken = default);
}
