using Microsoft.Extensions.Logging;
using Olbrasoft.GitHub.Issues.Data;
using Olbrasoft.GitHub.Issues.Data.Dtos;
using Olbrasoft.GitHub.Issues.Data.Entities;
using Olbrasoft.GitHub.Issues.Data.Repositories;
using Olbrasoft.Text.Transformation.Abstractions;

namespace Olbrasoft.GitHub.Issues.Business.Services;

/// <summary>
/// Service for generating issue summaries with translation.
/// Orchestrates: cache check → summarization (LLM) → translation → cache save → notification (SignalR).
/// </summary>
public class IssueSummaryService : IIssueSummaryService
{
    private readonly ICachedTextRepository _cachedTextRepository;
    private readonly ISummarizationService _summarizationService;
    private readonly ITranslationFallbackService _translationService;
    private readonly ISummaryNotifier _summaryNotifier;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<IssueSummaryService> _logger;

    public IssueSummaryService(
        ICachedTextRepository cachedTextRepository,
        ISummarizationService summarizationService,
        ITranslationFallbackService translationService,
        ISummaryNotifier summaryNotifier,
        TimeProvider timeProvider,
        ILogger<IssueSummaryService> logger)
    {
        _cachedTextRepository = cachedTextRepository;
        _summarizationService = summarizationService;
        _translationService = translationService;
        _summaryNotifier = summaryNotifier;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task GenerateSummaryAsync(int issueId, string body, string language, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[IssueSummaryService] START for issue {Id}, language={Language}", issueId, language);

        // Get issue for cache timestamp validation
        var issue = await _cachedTextRepository.GetIssueByIdAsync(issueId, cancellationToken);
        if (issue == null)
        {
            _logger.LogWarning("[IssueSummaryService] Issue {Id} not found", issueId);
            return;
        }

        // Determine which languages to process
        var processEnglish = language is "en" or "both";
        var processCzech = language is "cs" or "both";

        // Check cache for English summary (ListSummary)
        var enLanguageId = (int)LanguageCode.EnUS;
        var csLanguageId = (int)LanguageCode.CsCZ;
        var listSummaryTypeId = (int)TextTypeCode.ListSummary;

        var cachedEnSummary = await GetCachedSummaryAsync(issueId, enLanguageId, listSummaryTypeId, issue.GitHubUpdatedAt.UtcDateTime, cancellationToken);
        var cachedCsSummary = processCzech ? await GetCachedSummaryAsync(issueId, csLanguageId, listSummaryTypeId, issue.GitHubUpdatedAt.UtcDateTime, cancellationToken) : null;

        // If all requested summaries are cached, send them and return
        if ((processEnglish && cachedEnSummary != null) && (!processCzech || cachedCsSummary != null))
        {
            _logger.LogInformation("[IssueSummaryService] Cache HIT for issue {Id} - serving from cache", issueId);

            if (processEnglish)
            {
                await _summaryNotifier.NotifySummaryReadyAsync(
                    new SummaryNotificationDto(issueId, cachedEnSummary, "cache", "en"),
                    cancellationToken);
            }

            if (processCzech && cachedCsSummary != null)
            {
                await _summaryNotifier.NotifySummaryReadyAsync(
                    new SummaryNotificationDto(issueId, cachedCsSummary, "cache", "cs"),
                    cancellationToken);
            }

            return;
        }

        _logger.LogInformation("[IssueSummaryService] Cache MISS for issue {Id} - generating summary", issueId);

        if (string.IsNullOrWhiteSpace(body))
        {
            _logger.LogWarning("[IssueSummaryService] Empty body for issue {Id} - cannot generate summary", issueId);
            return;
        }

        // Step 1: Generate English summary (or use cached)
        string englishSummary;
        string enProvider;

        if (cachedEnSummary != null)
        {
            englishSummary = cachedEnSummary;
            enProvider = "cache";
            _logger.LogInformation("[IssueSummaryService] Using cached English summary for issue {Id}", issueId);
        }
        else
        {
            _logger.LogInformation("[IssueSummaryService] Calling AI summarization...");
            var summarizeResult = await _summarizationService.SummarizeAsync(body, cancellationToken);
            if (!summarizeResult.Success || string.IsNullOrWhiteSpace(summarizeResult.Summary))
            {
                _logger.LogWarning("[IssueSummaryService] Summarization failed for issue {Id}: {Error}", issueId, summarizeResult.Error);
                return;
            }

            englishSummary = summarizeResult.Summary;
            enProvider = $"{summarizeResult.Provider}/{summarizeResult.Model}";
            _logger.LogInformation("[IssueSummaryService] Summarization succeeded via {Provider}", enProvider);

            // Save to cache
            await SaveToCacheAsync(issueId, enLanguageId, listSummaryTypeId, englishSummary, cancellationToken);
        }

        // Send English summary if requested
        if (processEnglish)
        {
            await _summaryNotifier.NotifySummaryReadyAsync(
                new SummaryNotificationDto(issueId, englishSummary, enProvider, "en"),
                cancellationToken);
        }

        // If only English requested, finish
        if (!processCzech)
        {
            _logger.LogInformation("[IssueSummaryService] COMPLETE (EN only) for issue {Id}", issueId);
            return;
        }

        // Step 2: Generate Czech summary (or use cached)
        if (cachedCsSummary != null)
        {
            await _summaryNotifier.NotifySummaryReadyAsync(
                new SummaryNotificationDto(issueId, cachedCsSummary, "cache", "cs"),
                cancellationToken);
            _logger.LogInformation("[IssueSummaryService] COMPLETE (CS from cache) for issue {Id}", issueId);
            return;
        }

        _logger.LogInformation("[IssueSummaryService] Calling Translation Service...");
        var translateResult = await _translationService.TranslateWithFallbackAsync(
            englishSummary,
            "cs",
            "en",
            cancellationToken);

        if (translateResult.Success && !string.IsNullOrWhiteSpace(translateResult.Translation))
        {
            var csProvider = $"{enProvider} → {translateResult.Provider}";
            _logger.LogInformation("[IssueSummaryService] Translation succeeded via {Provider}", translateResult.Provider);

            // Save to cache
            await SaveToCacheAsync(issueId, csLanguageId, listSummaryTypeId, translateResult.Translation, cancellationToken);

            // Send translated summary
            await _summaryNotifier.NotifySummaryReadyAsync(
                new SummaryNotificationDto(issueId, translateResult.Translation, csProvider, "cs"),
                cancellationToken);

            _logger.LogInformation("[IssueSummaryService] COMPLETE for issue {Id} via {Provider}", issueId, csProvider);
        }
        else
        {
            _logger.LogWarning("[IssueSummaryService] Translation failed for issue {Id}: {Error}. Using English fallback.", issueId, translateResult.Error);

            if (language == "cs")
            {
                await _summaryNotifier.NotifySummaryReadyAsync(
                    new SummaryNotificationDto(issueId, englishSummary, enProvider + " (EN fallback)", "en"),
                    cancellationToken);
            }
            else if (language == "both")
            {
                await _summaryNotifier.NotifySummaryReadyAsync(
                    new SummaryNotificationDto(issueId, englishSummary, enProvider + " (překlad nedostupný)", "cs"),
                    cancellationToken);
            }

            _logger.LogInformation("[IssueSummaryService] COMPLETE (EN fallback) for issue {Id}", issueId);
        }
    }

    /// <summary>
    /// Gets cached summary if exists and is fresh.
    /// </summary>
    private async Task<string?> GetCachedSummaryAsync(int issueId, int languageId, int textTypeId, DateTime issueUpdatedAt, CancellationToken ct)
    {
        var cached = await _cachedTextRepository.GetByIssueAsync(issueId, languageId, textTypeId, ct);

        if (cached == null) return null;

        // Validate freshness
        if (issueUpdatedAt > cached.CachedAt)
        {
            _logger.LogDebug("[IssueSummaryService] Cache STALE for issue {IssueId}, language {LangId}", issueId, languageId);
            await _cachedTextRepository.DeleteAsync(cached, ct);
            return null;
        }

        return cached.Content;
    }

    /// <summary>
    /// Saves summary to cache.
    /// </summary>
    private async Task SaveToCacheAsync(int issueId, int languageId, int textTypeId, string content, CancellationToken ct)
    {
        await _cachedTextRepository.SaveAsync(new CachedText
        {
            IssueId = issueId,
            LanguageId = languageId,
            TextTypeId = textTypeId,
            Content = content,
            CachedAt = _timeProvider.GetUtcNow().UtcDateTime
        }, ct);

        _logger.LogDebug("[IssueSummaryService] Saved to cache: Issue {IssueId}, Language {LangId}, Type {TypeId}", issueId, languageId, textTypeId);
    }
}
