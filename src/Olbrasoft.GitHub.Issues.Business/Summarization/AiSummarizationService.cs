using Microsoft.Extensions.Logging;
using Olbrasoft.Text.Transformation.Abstractions;

namespace Olbrasoft.GitHub.Issues.Business.Summarization;

/// <summary>
/// Implementation of AI summarization service.
/// Wraps ISummarizationService and provides domain-specific summarization logic.
/// </summary>
public class AiSummarizationService : IAiSummarizationService
{
    private readonly ISummarizationService _summarizationService;
    private readonly ILogger<AiSummarizationService> _logger;

    public AiSummarizationService(
        ISummarizationService summarizationService,
        ILogger<AiSummarizationService> logger)
    {
        ArgumentNullException.ThrowIfNull(summarizationService);
        ArgumentNullException.ThrowIfNull(logger);

        _summarizationService = summarizationService;
        _logger = logger;
    }

    public async Task<AiSummarizationResult> GenerateSummaryAsync(
        string body,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            _logger.LogWarning("[AiSummarizationService] Empty body provided - cannot generate summary");
            return new AiSummarizationResult(
                Success: false,
                Summary: null,
                Provider: null,
                Error: "Body is empty or whitespace");
        }

        try
        {
            _logger.LogDebug("[AiSummarizationService] Calling AI summarization service...");

            var result = await _summarizationService.SummarizeAsync(body, cancellationToken);

            if (!result.Success || string.IsNullOrWhiteSpace(result.Summary))
            {
                _logger.LogWarning(
                    "[AiSummarizationService] Summarization failed: {Error}",
                    result.Error);

                return new AiSummarizationResult(
                    Success: false,
                    Summary: null,
                    Provider: result.Provider,
                    Error: result.Error ?? "Summarization failed with no error message");
            }

            var providerInfo = $"{result.Provider}/{result.Model}";
            _logger.LogInformation(
                "[AiSummarizationService] Summarization succeeded via {Provider}",
                providerInfo);

            return new AiSummarizationResult(
                Success: true,
                Summary: result.Summary,
                Provider: providerInfo,
                Error: null);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "[AiSummarizationService] Exception during summarization");

            return new AiSummarizationResult(
                Success: false,
                Summary: null,
                Provider: null,
                Error: $"Exception: {ex.Message}");
        }
    }
}
