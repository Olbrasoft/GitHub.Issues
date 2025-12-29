using Microsoft.Extensions.Logging;
using Olbrasoft.GitHub.Issues.Business.Summarization;
using Olbrasoft.GitHub.Issues.Data;
using Olbrasoft.GitHub.Issues.Data.Entities;
using Olbrasoft.GitHub.Issues.Data.Repositories;
using Olbrasoft.Text.Transformation.Abstractions;
using Olbrasoft.Text.Translation;

namespace Olbrasoft.GitHub.Issues.Business.Translation;

/// <summary>
/// Service for retrieving translated/cached text with cache-first strategy.
/// Checks cache first, generates on miss, validates freshness via timestamps.
/// </summary>
public class TranslatedTextService : ITranslatedTextService
{
    private readonly ICachedTextRepository _repository;
    private readonly ITranslator _translator;
    private readonly ISummarizationService _summarizer;
    private readonly ITranslationCacheService _cacheService;
    private readonly ILogger<TranslatedTextService> _logger;

    public TranslatedTextService(
        ICachedTextRepository repository,
        ITranslator translator,
        ISummarizationService summarizer,
        ITranslationCacheService cacheService,
        ILogger<TranslatedTextService> logger)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(translator);
        ArgumentNullException.ThrowIfNull(summarizer);
        ArgumentNullException.ThrowIfNull(cacheService);
        ArgumentNullException.ThrowIfNull(logger);

        _repository = repository;
        _translator = translator;
        _summarizer = summarizer;
        _cacheService = cacheService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<string> GetTitleAsync(int issueId, int languageId, CancellationToken ct = default)
    {
        // English doesn't need title translation
        if (languageId.IsEnglish())
        {
            var issue = await _repository.GetIssueByIdAsync(issueId, ct);
            return issue?.Title ?? string.Empty;
        }

        return await GetCachedOrGenerateAsync(
            issueId,
            languageId,
            TextTypeCode.Title,
            async (issue, lang) =>
            {
                var targetLang = GetLanguageCulture(languageId);
                var result = await _translator.TranslateAsync(issue.Title, targetLang, "en", ct);
                return result.Success ? result.Translation ?? issue.Title : issue.Title;
            },
            ct);
    }

    /// <inheritdoc />
    public async Task<string> GetListSummaryAsync(int issueId, int languageId, CancellationToken ct = default)
    {
        return await GetCachedOrGenerateAsync(
            issueId,
            languageId,
            TextTypeCode.ListSummary,
            async (issue, lang) =>
            {
                // Step 1: Summarize in English
                var summaryResult = await _summarizer.SummarizeAsync(issue.Title, ct);
                if (!summaryResult.Success || string.IsNullOrWhiteSpace(summaryResult.Summary))
                {
                    _logger.LogWarning("[TranslatedTextService] Summarization failed for Issue {IssueId}", issue.Id);
                    return string.Empty;
                }

                // Step 2: Translate to target language if not English
                if (languageId.IsEnglish())
                {
                    return summaryResult.Summary;
                }

                var targetLang = GetLanguageCulture(languageId);
                var translateResult = await _translator.TranslateAsync(summaryResult.Summary, targetLang, "en", ct);
                return translateResult.Success ? translateResult.Translation ?? summaryResult.Summary : summaryResult.Summary;
            },
            ct);
    }

    /// <inheritdoc />
    public async Task<string> GetDetailSummaryAsync(int issueId, int languageId, CancellationToken ct = default)
    {
        return await GetCachedOrGenerateAsync(
            issueId,
            languageId,
            TextTypeCode.DetailSummary,
            async (issue, lang) =>
            {
                // Step 1: Summarize in English (use full body for detail summary)
                var summaryResult = await _summarizer.SummarizeAsync(issue.Title, ct);
                if (!summaryResult.Success || string.IsNullOrWhiteSpace(summaryResult.Summary))
                {
                    _logger.LogWarning("[TranslatedTextService] Summarization failed for Issue {IssueId}", issue.Id);
                    return string.Empty;
                }

                // Step 2: Translate to target language if not English
                if (languageId.IsEnglish())
                {
                    return summaryResult.Summary;
                }

                var targetLang = GetLanguageCulture(languageId);
                var translateResult = await _translator.TranslateAsync(summaryResult.Summary, targetLang, "en", ct);
                return translateResult.Success ? translateResult.Translation ?? summaryResult.Summary : summaryResult.Summary;
            },
            ct);
    }

    /// <inheritdoc />
    public async Task<Dictionary<int, (string Title, string Summary)>> GetForListAsync(
        IEnumerable<int> issueIds,
        int languageId,
        CancellationToken ct = default)
    {
        var ids = issueIds.ToList();
        var result = new Dictionary<int, (string Title, string Summary)>();

        if (ids.Count == 0) return result;

        // Load issues for timestamp validation
        var issues = await _repository.GetIssuesByIdsAsync(ids, ct);

        // Load all cached in one query
        var cached = await _repository.GetMultipleCachedTextsAsync(
            ids,
            languageId,
            new[] { (int)TextTypeCode.Title, (int)TextTypeCode.ListSummary },
            ct);

        var staleIssueIds = new List<int>();

        foreach (var issueId in ids)
        {
            if (!issues.TryGetValue(issueId, out var issue)) continue;

            var titleCache = cached.FirstOrDefault(c => c.IssueId == issueId && c.TextTypeId == (int)TextTypeCode.Title);
            var summaryCache = cached.FirstOrDefault(c => c.IssueId == issueId && c.TextTypeId == (int)TextTypeCode.ListSummary);

            // Check for stale cache (timestamp validation)
            var issueUpdatedAt = issue.GitHubUpdatedAt.UtcDateTime;
            if ((titleCache != null && issueUpdatedAt > titleCache.CachedAt) ||
                (summaryCache != null && issueUpdatedAt > summaryCache.CachedAt))
            {
                _logger.LogDebug(
                    "[TranslatedTextService] Stale cache detected for Issue {IssueId}",
                    issueId);
                staleIssueIds.Add(issueId);
                titleCache = null;
                summaryCache = null;
            }

            string title;
            string summary;

            // Get title
            if (languageId.IsEnglish())
            {
                title = issue.Title;
            }
            else if (titleCache != null)
            {
                title = titleCache.Content;
                _logger.LogDebug("[TranslatedTextService] Cache HIT: Title for Issue {IssueId}", issueId);
            }
            else
            {
                title = await GenerateAndCacheAsync(issue, languageId, TextTypeCode.Title, ct);
            }

            // Get summary
            if (summaryCache != null)
            {
                summary = summaryCache.Content;
                _logger.LogDebug("[TranslatedTextService] Cache HIT: Summary for Issue {IssueId}", issueId);
            }
            else
            {
                summary = await GenerateAndCacheAsync(issue, languageId, TextTypeCode.ListSummary, ct);
            }

            result[issueId] = (title, summary);
        }

        // Invalidate stale caches
        foreach (var staleId in staleIssueIds)
        {
            await _cacheService.InvalidateAsync(staleId, ct);
        }

        return result;
    }

    private async Task<string> GetCachedOrGenerateAsync(
        int issueId,
        int languageId,
        TextTypeCode textType,
        Func<Issue, int, Task<string>> generator,
        CancellationToken ct)
    {
        // Get issue for timestamp validation
        var issue = await _repository.GetIssueByIdAsync(issueId, ct);
        if (issue == null) return string.Empty;

        // Check cache first
        var cached = await _repository.GetByIssueAsync(issueId, languageId, (int)textType, ct);

        // Validate cache freshness (fallback for missed webhooks)
        if (cached != null)
        {
            var issueUpdatedAt = issue.GitHubUpdatedAt.UtcDateTime;
            if (issueUpdatedAt > cached.CachedAt)
            {
                // Cache is stale - webhook was probably missed
                _logger.LogWarning(
                    "[TranslatedTextService] Stale cache detected for Issue {IssueId}, Language {LanguageId}, TextType {TextType}. " +
                    "Issue updated {IssueUpdated}, cache created {CacheCreated}. Invalidating.",
                    issueId, languageId, textType, issueUpdatedAt, cached.CachedAt);

                await _cacheService.InvalidateAsync(issueId, ct);
                cached = null;
            }
            else
            {
                _logger.LogDebug(
                    "[TranslatedTextService] Cache HIT: {TextType} for Issue {IssueId}, Language {LanguageId}",
                    textType, issueId, languageId);
                return cached.Content;
            }
        }

        // Cache miss or stale - generate and store
        _logger.LogDebug(
            "[TranslatedTextService] Cache MISS: {TextType} for Issue {IssueId}, Language {LanguageId}",
            textType, issueId, languageId);

        var generated = await generator(issue, languageId);

        // Store in cache (handle concurrent insert)
        await SaveToCacheAsync(issueId, languageId, textType, generated, ct);

        return generated;
    }

    private async Task<string> GenerateAndCacheAsync(
        Issue issue,
        int languageId,
        TextTypeCode textType,
        CancellationToken ct)
    {
        string generated;

        if (textType == TextTypeCode.Title)
        {
            // Title translation only (no summarization)
            if (languageId.IsEnglish())
            {
                generated = issue.Title;
            }
            else
            {
                var targetLang = GetLanguageCulture(languageId);
                var result = await _translator.TranslateAsync(issue.Title, targetLang, "en", ct);
                generated = result.Success ? result.Translation ?? issue.Title : issue.Title;
            }
        }
        else
        {
            // Summary: Summarize in English, then translate if needed
            var summaryResult = await _summarizer.SummarizeAsync(issue.Title, ct);
            if (!summaryResult.Success || string.IsNullOrWhiteSpace(summaryResult.Summary))
            {
                _logger.LogWarning("[TranslatedTextService] Summarization failed for Issue {IssueId}", issue.Id);
                generated = string.Empty;
            }
            else if (languageId.IsEnglish())
            {
                generated = summaryResult.Summary;
            }
            else
            {
                var targetLang = GetLanguageCulture(languageId);
                var translateResult = await _translator.TranslateAsync(summaryResult.Summary, targetLang, "en", ct);
                generated = translateResult.Success ? translateResult.Translation ?? summaryResult.Summary : summaryResult.Summary;
            }
        }

        await SaveToCacheAsync(issue.Id, languageId, textType, generated, ct);

        return generated;
    }

    private async Task SaveToCacheAsync(
        int issueId,
        int languageId,
        TextTypeCode textType,
        string content,
        CancellationToken ct)
    {
        var cachedText = new CachedText
        {
            IssueId = issueId,
            LanguageId = languageId,
            TextTypeId = (int)textType,
            Content = content,
            CachedAt = DateTime.UtcNow
        };

        await _repository.SaveOrUpdateAsync(cachedText, ct);
    }

    private static string GetLanguageCulture(int languageId)
    {
        try
        {
            return System.Globalization.CultureInfo.GetCultureInfo(languageId).TwoLetterISOLanguageName;
        }
        catch
        {
            return "en";
        }
    }
}
