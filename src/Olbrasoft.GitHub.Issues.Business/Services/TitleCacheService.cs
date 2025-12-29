using Microsoft.Extensions.Logging;
using Olbrasoft.GitHub.Issues.Data;
using Olbrasoft.GitHub.Issues.Data.Entities;
using Olbrasoft.GitHub.Issues.Data.Repositories;

namespace Olbrasoft.GitHub.Issues.Business.Services;

/// <summary>
/// Service for managing title translation cache operations.
/// Handles cache retrieval, freshness validation, and storage.
/// </summary>
public class TitleCacheService : ITitleCacheService
{
    private readonly ITranslationRepository _translationRepository;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<TitleCacheService> _logger;

    public TitleCacheService(
        ITranslationRepository translationRepository,
        TimeProvider timeProvider,
        ILogger<TitleCacheService> logger)
    {
        ArgumentNullException.ThrowIfNull(translationRepository);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);

        _translationRepository = translationRepository;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<string?> GetCachedTitleAsync(
        int issueId,
        int languageId,
        DateTime issueUpdatedAt,
        CancellationToken cancellationToken = default)
    {
        var cached = await _translationRepository.GetCachedTranslationAsync(
            issueId,
            languageId,
            (int)TextTypeCode.Title,
            cancellationToken);

        if (cached == null)
        {
            return null;
        }

        // Validate freshness - if issue was updated after cache, invalidate
        if (issueUpdatedAt > cached.CachedAt)
        {
            _logger.LogDebug(
                "[TitleCacheService] Cache STALE for issue {IssueId}, language {LangId} - deleting",
                issueId,
                languageId);

            await _translationRepository.DeleteCachedTextAsync(cached, cancellationToken);
            return null;
        }

        _logger.LogDebug(
            "[TitleCacheService] Cache HIT for issue {IssueId}, language {LangId}",
            issueId,
            languageId);

        return cached.Content;
    }

    /// <inheritdoc />
    public async Task SaveTitleAsync(
        int issueId,
        int languageId,
        string content,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            _logger.LogDebug(
                "[TitleCacheService] Skipping cache save due to empty content: Issue {IssueId}, Language {LangId}",
                issueId,
                languageId);

            return;
        }

        var cachedText = new CachedText
        {
            IssueId = issueId,
            LanguageId = languageId,
            TextTypeId = (int)TextTypeCode.Title,
            Content = content,
            CachedAt = _timeProvider.GetUtcNow().UtcDateTime
        };

        await _translationRepository.SaveCachedTextAsync(cachedText, cancellationToken);

        _logger.LogDebug(
            "[TitleCacheService] Saved to cache: Issue {IssueId}, Language {LangId}",
            issueId,
            languageId);
    }
}
