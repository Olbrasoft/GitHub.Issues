using Microsoft.Extensions.Logging;
using Olbrasoft.GitHub.Issues.Data.Entities;
using Olbrasoft.GitHub.Issues.Data.Repositories;

namespace Olbrasoft.GitHub.Issues.Business.Services;

/// <summary>
/// Service for managing summary cache operations.
/// Handles cache retrieval, freshness validation, and storage.
/// </summary>
public class SummaryCacheService : ISummaryCacheService
{
    private readonly ICachedTextRepository _cachedTextRepository;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<SummaryCacheService> _logger;

    public SummaryCacheService(
        ICachedTextRepository cachedTextRepository,
        TimeProvider timeProvider,
        ILogger<SummaryCacheService> logger)
    {
        _cachedTextRepository = cachedTextRepository ?? throw new ArgumentNullException(nameof(cachedTextRepository));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<string?> GetCachedSummaryAsync(
        int issueId,
        int languageId,
        int textTypeId,
        DateTime issueUpdatedAt,
        CancellationToken cancellationToken = default)
    {
        var cached = await _cachedTextRepository.GetByIssueAsync(issueId, languageId, textTypeId, cancellationToken);

        if (cached == null)
        {
            return null;
        }

        // Validate freshness - if issue was updated after cache, invalidate
        if (issueUpdatedAt > cached.CachedAt)
        {
            _logger.LogDebug(
                "[SummaryCacheService] Cache STALE for issue {IssueId}, language {LangId} - deleting",
                issueId,
                languageId);

            await _cachedTextRepository.DeleteAsync(cached, cancellationToken);
            return null;
        }

        _logger.LogDebug(
            "[SummaryCacheService] Cache HIT for issue {IssueId}, language {LangId}",
            issueId,
            languageId);

        return cached.Content;
    }

    /// <inheritdoc />
    public async Task SaveSummaryAsync(
        int issueId,
        int languageId,
        int textTypeId,
        string content,
        CancellationToken cancellationToken = default)
    {
        await _cachedTextRepository.SaveAsync(new CachedText
        {
            IssueId = issueId,
            LanguageId = languageId,
            TextTypeId = textTypeId,
            Content = content,
            CachedAt = _timeProvider.GetUtcNow().UtcDateTime
        }, cancellationToken);

        _logger.LogDebug(
            "[SummaryCacheService] Saved to cache: Issue {IssueId}, Language {LangId}, Type {TypeId}",
            issueId,
            languageId,
            textTypeId);
    }
}
