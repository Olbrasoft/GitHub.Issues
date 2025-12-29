using Microsoft.Extensions.Logging;
using Olbrasoft.GitHub.Issues.Business.Translation;
using Olbrasoft.GitHub.Issues.Data;
using Olbrasoft.GitHub.Issues.Data.Repositories;

namespace Olbrasoft.GitHub.Issues.Business.Summarization;

/// <summary>
/// Orchestrator that coordinates the complete summary workflow.
/// Delegates to specialized services following SRP.
/// Part of #310 IssueSummaryService refactoring - FINAL STEP (#320).
/// </summary>
public class IssueSummaryOrchestrator : IIssueSummaryOrchestrator
{
    private readonly ICachedTextRepository _repository;
    private readonly ISummaryCacheService _cacheService;
    private readonly IAiSummarizationService _aiService;
    private readonly ITranslationFallbackService _translationService;
    private readonly ISummaryNotificationService _notificationService;
    private readonly ILogger<IssueSummaryOrchestrator> _logger;

    public IssueSummaryOrchestrator(
        ICachedTextRepository repository,
        ISummaryCacheService cacheService,
        IAiSummarizationService aiService,
        ITranslationFallbackService translationService,
        ISummaryNotificationService notificationService,
        ILogger<IssueSummaryOrchestrator> logger)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(cacheService);
        ArgumentNullException.ThrowIfNull(aiService);
        ArgumentNullException.ThrowIfNull(translationService);
        ArgumentNullException.ThrowIfNull(notificationService);
        ArgumentNullException.ThrowIfNull(logger);

        _repository = repository;
        _cacheService = cacheService;
        _aiService = aiService;
        _translationService = translationService;
        _notificationService = notificationService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task GenerateSummaryFromBodyAsync(
        int issueId,
        string body,
        string language,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "[IssueSummaryOrchestrator] START for issue {IssueId}, language={Language}",
            issueId, language);

        // Get issue for cache freshness validation
        var issue = await _repository.GetIssueByIdAsync(issueId, cancellationToken);
        if (issue == null)
        {
            _logger.LogWarning("[IssueSummaryOrchestrator] Issue {IssueId} not found", issueId);
            return;
        }

        // Determine which languages to process
        var processEnglish = language is "en" or "both";
        var processCzech = language is "cs" or "both";

        // 1. Try cache first
        var enLanguageCode = (int)LanguageCode.EnUS;
        var csLanguageCode = (int)LanguageCode.CsCZ;

        var cachedEn = processEnglish
            ? await _cacheService.GetIfFreshAsync(issueId, enLanguageCode, issue.GitHubUpdatedAt.UtcDateTime, cancellationToken)
            : null;

        var cachedCs = processCzech
            ? await _cacheService.GetIfFreshAsync(issueId, csLanguageCode, issue.GitHubUpdatedAt.UtcDateTime, cancellationToken)
            : null;

        // If all requested summaries cached, send them and return
        if ((processEnglish && cachedEn != null) && (!processCzech || cachedCs != null))
        {
            _logger.LogInformation("[IssueSummaryOrchestrator] Cache HIT - serving from cache");

            if (processEnglish && cachedEn != null)
            {
                await _notificationService.NotifySummaryAsync(issueId, cachedEn, "cache", "en", cancellationToken);
            }

            if (processCzech && cachedCs != null)
            {
                await _notificationService.NotifySummaryAsync(issueId, cachedCs, "cache", "cs", cancellationToken);
            }

            return;
        }

        // Validate body
        if (string.IsNullOrWhiteSpace(body))
        {
            _logger.LogWarning("[IssueSummaryOrchestrator] Empty body for issue {IssueId}", issueId);
            return;
        }

        // 2. Generate English summary (or use cached)
        string englishSummary;
        string enProvider;

        if (cachedEn != null)
        {
            englishSummary = cachedEn;
            enProvider = "cache";
            _logger.LogInformation("[IssueSummaryOrchestrator] Using cached English summary");
        }
        else
        {
            _logger.LogInformation("[IssueSummaryOrchestrator] Generating AI summary...");
            var aiResult = await _aiService.GenerateSummaryAsync(body, cancellationToken);

            if (!aiResult.Success || string.IsNullOrWhiteSpace(aiResult.Summary))
            {
                _logger.LogWarning(
                    "[IssueSummaryOrchestrator] AI summarization failed: {Error}",
                    aiResult.Error);
                return;
            }

            englishSummary = aiResult.Summary;
            enProvider = aiResult.Provider ?? "Unknown";
            _logger.LogInformation("[IssueSummaryOrchestrator] AI summarization succeeded via {Provider}", enProvider);

            // Save to cache
            await _cacheService.SaveAsync(issueId, enLanguageCode, englishSummary, cancellationToken);
        }

        // Send English summary if requested
        if (processEnglish)
        {
            await _notificationService.NotifySummaryAsync(issueId, englishSummary, enProvider, "en", cancellationToken);
        }

        // If only English requested, finish
        if (!processCzech)
        {
            _logger.LogInformation("[IssueSummaryOrchestrator] COMPLETE (EN only) for issue {IssueId}", issueId);
            return;
        }

        // 3. Generate Czech summary (or use cached)
        if (cachedCs != null)
        {
            await _notificationService.NotifySummaryAsync(issueId, cachedCs, "cache", "cs", cancellationToken);
            _logger.LogInformation("[IssueSummaryOrchestrator] COMPLETE (CS from cache)");
            return;
        }

        // Translate English → Czech
        _logger.LogInformation("[IssueSummaryOrchestrator] Translating to Czech...");
        var translationResult = await _translationService.TranslateWithFallbackAsync(
            englishSummary,
            "cs",
            "en",
            cancellationToken);

        if (translationResult.Success && !string.IsNullOrWhiteSpace(translationResult.Translation))
        {
            var csProvider = $"{enProvider} → {translationResult.Provider}";
            _logger.LogInformation("[IssueSummaryOrchestrator] Translation succeeded via {Provider}", translationResult.Provider);

            // Save Czech to cache
            await _cacheService.SaveAsync(issueId, csLanguageCode, translationResult.Translation, cancellationToken);

            // Send Czech summary
            await _notificationService.NotifySummaryAsync(issueId, translationResult.Translation, csProvider, "cs", cancellationToken);

            _logger.LogInformation("[IssueSummaryOrchestrator] COMPLETE for issue {IssueId}", issueId);
        }
        else
        {
            _logger.LogWarning(
                "[IssueSummaryOrchestrator] Translation failed: {Error}. Using English fallback.",
                translationResult.Error);

            // Send English as fallback
            if (language == "cs")
            {
                await _notificationService.NotifySummaryAsync(issueId, englishSummary, enProvider + " (EN fallback)", "en", cancellationToken);
            }
            else if (language == "both")
            {
                await _notificationService.NotifySummaryAsync(issueId, englishSummary, enProvider + " (překlad nedostupný)", "cs", cancellationToken);
            }

            _logger.LogInformation("[IssueSummaryOrchestrator] COMPLETE (EN fallback) for issue {IssueId}", issueId);
        }
    }

    /// <inheritdoc />
    public async Task GenerateSummaryAsync(int issueId, CancellationToken cancellationToken = default)
    {
        await GenerateSummaryAsync(issueId, "both", cancellationToken);
    }

    /// <inheritdoc />
    public async Task GenerateSummaryAsync(int issueId, string language, CancellationToken cancellationToken = default)
    {
        // Get issue with body from repository
        var issue = await _repository.GetIssueByIdAsync(issueId, cancellationToken);
        if (issue == null)
        {
            _logger.LogWarning("[IssueSummaryOrchestrator] Issue {IssueId} not found", issueId);
            return;
        }

        // Note: ICachedTextRepository doesn't have Body property exposed
        // This method delegates to IIssueSummaryService which has the existing logic
        // For now, log warning - proper implementation would need repository enhancement
        _logger.LogWarning(
            "[IssueSummaryOrchestrator] GenerateSummaryAsync(issueId, language) requires body fetch - not yet implemented");
    }

    /// <inheritdoc />
    public async Task GenerateSummariesAsync(
        IEnumerable<(int IssueId, string Body)> issuesWithBodies,
        string language = "en",
        CancellationToken cancellationToken = default)
    {
        foreach (var (issueId, body) in issuesWithBodies)
        {
            await GenerateSummaryFromBodyAsync(issueId, body, language, cancellationToken);
        }
    }
}
