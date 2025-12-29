using Microsoft.Extensions.Logging;

namespace Olbrasoft.GitHub.Issues.Business.Summarization;

/// <summary>
/// Service for generating issue summaries with translation.
/// REFACTORED (#310, #320): Now delegates to IssueSummaryOrchestrator.
/// Kept for backward compatibility - consider using IIssueSummaryOrchestrator directly.
/// </summary>
public class IssueSummaryService : IIssueSummaryService
{
    private readonly IIssueSummaryOrchestrator _orchestrator;
    private readonly ILogger<IssueSummaryService> _logger;

    public IssueSummaryService(
        IIssueSummaryOrchestrator orchestrator,
        ILogger<IssueSummaryService> logger)
    {
        ArgumentNullException.ThrowIfNull(orchestrator);
        ArgumentNullException.ThrowIfNull(logger);

        _orchestrator = orchestrator;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task GenerateSummaryAsync(int issueId, string body, string language, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "[IssueSummaryService] Delegating to orchestrator for issue {IssueId}, language={Language}",
            issueId, language);

        await _orchestrator.GenerateSummaryFromBodyAsync(issueId, body, language, cancellationToken);
    }
}
