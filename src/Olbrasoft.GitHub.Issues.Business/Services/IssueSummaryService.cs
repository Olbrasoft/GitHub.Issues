using Microsoft.Extensions.Logging;
using Olbrasoft.GitHub.Issues.Data.Dtos;
using Olbrasoft.Text.Transformation.Abstractions;

namespace Olbrasoft.GitHub.Issues.Business.Services;

/// <summary>
/// Service for generating issue summaries with translation.
/// Orchestrates summarization (LLM) → translation (TranslationFallbackService) → notification (SignalR).
/// </summary>
public class IssueSummaryService : IIssueSummaryService
{
    private readonly ISummarizationService _summarizationService;
    private readonly ITranslationFallbackService _translationService;
    private readonly ISummaryNotifier _summaryNotifier;
    private readonly ILogger<IssueSummaryService> _logger;

    public IssueSummaryService(
        ISummarizationService summarizationService,
        ITranslationFallbackService translationService,
        ISummaryNotifier summaryNotifier,
        ILogger<IssueSummaryService> logger)
    {
        _summarizationService = summarizationService;
        _translationService = translationService;
        _summaryNotifier = summaryNotifier;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task GenerateSummaryAsync(int issueId, string body, string language, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[IssueSummaryService] START for issue {Id}, language={Language}", issueId, language);

        if (string.IsNullOrWhiteSpace(body))
        {
            _logger.LogWarning("[IssueSummaryService] Empty body for issue {Id} - cannot generate summary", issueId);
            return;
        }

        // Step 1: Summarize in English
        _logger.LogInformation("[IssueSummaryService] Calling AI summarization...");
        var summarizeResult = await _summarizationService.SummarizeAsync(body, cancellationToken);
        if (!summarizeResult.Success || string.IsNullOrWhiteSpace(summarizeResult.Summary))
        {
            _logger.LogWarning("[IssueSummaryService] Summarization failed for issue {Id}: {Error}", issueId, summarizeResult.Error);
            return;
        }
        _logger.LogInformation("[IssueSummaryService] Summarization succeeded via {Provider}/{Model}", summarizeResult.Provider, summarizeResult.Model);

        var enProvider = $"{summarizeResult.Provider}/{summarizeResult.Model}";

        // Send English summary if requested
        if (language is "en" or "both")
        {
            _logger.LogInformation("[IssueSummaryService] Sending English summary via SignalR...");
            await _summaryNotifier.NotifySummaryReadyAsync(
                new SummaryNotificationDto(issueId, summarizeResult.Summary, enProvider, "en"),
                cancellationToken);
        }

        // If only English requested, finish
        if (language == "en")
        {
            _logger.LogInformation("[IssueSummaryService] COMPLETE (EN only) for issue {Id}", issueId);
            return;
        }

        // Step 2: Translate to target language
        _logger.LogInformation("[IssueSummaryService] Calling Translation Service...");
        var translateResult = await _translationService.TranslateWithFallbackAsync(
            summarizeResult.Summary,
            "cs",
            "en",
            cancellationToken);

        if (translateResult.Success && !string.IsNullOrWhiteSpace(translateResult.Translation))
        {
            var csProvider = $"{enProvider} → {translateResult.Provider}";
            _logger.LogInformation("[IssueSummaryService] Translation succeeded via {Provider}", translateResult.Provider);

            // Send translated summary
            await _summaryNotifier.NotifySummaryReadyAsync(
                new SummaryNotificationDto(issueId, translateResult.Translation, csProvider, "cs"),
                cancellationToken);

            _logger.LogInformation("[IssueSummaryService] COMPLETE for issue {Id} via {Provider}", issueId, csProvider);
        }
        else
        {
            // All translation attempts failed - use English summary as fallback
            _logger.LogWarning("[IssueSummaryService] All translation attempts failed for issue {Id}: {Error}. Using English summary.", issueId, translateResult.Error);

            // Send English as fallback for Czech language modes
            if (language == "cs")
            {
                // For "cs": We haven't sent English yet, send it now
                await _summaryNotifier.NotifySummaryReadyAsync(
                    new SummaryNotificationDto(issueId, summarizeResult.Summary, enProvider + " (EN fallback)", "en"),
                    cancellationToken);
            }
            else if (language == "both")
            {
                // For "both": We sent English already but client hid it (waiting for Czech)
                // Send English text but with "cs" language marker so client shows it in Czech div
                await _summaryNotifier.NotifySummaryReadyAsync(
                    new SummaryNotificationDto(issueId, summarizeResult.Summary, enProvider + " (překlad nedostupný)", "cs"),
                    cancellationToken);
            }

            _logger.LogInformation("[IssueSummaryService] COMPLETE (EN fallback) for issue {Id}", issueId);
        }
    }
}
