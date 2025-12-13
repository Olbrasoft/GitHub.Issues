using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore;

namespace Olbrasoft.GitHub.Issues.Business.Services;

/// <summary>
/// Service for managing translation cache invalidation.
/// </summary>
public class TranslationCacheService : ITranslationCacheService
{
    private readonly GitHubDbContext _context;
    private readonly ILogger<TranslationCacheService> _logger;

    public TranslationCacheService(
        GitHubDbContext context,
        ILogger<TranslationCacheService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<int> InvalidateAsync(int issueId, CancellationToken ct = default)
    {
        var deleted = await _context.CachedTexts
            .Where(t => t.IssueId == issueId)
            .ExecuteDeleteAsync(ct);

        if (deleted > 0)
        {
            _logger.LogInformation(
                "[TranslationCache] Invalidated {Count} cached translations for Issue {IssueId}",
                deleted, issueId);
        }

        return deleted;
    }

    /// <inheritdoc />
    public async Task<int> InvalidateByTextTypeAsync(int issueId, int textTypeId, CancellationToken ct = default)
    {
        var deleted = await _context.CachedTexts
            .Where(t => t.IssueId == issueId && t.TextTypeId == textTypeId)
            .ExecuteDeleteAsync(ct);

        if (deleted > 0)
        {
            _logger.LogInformation(
                "[TranslationCache] Invalidated {Count} cached translations for Issue {IssueId}, TextType {TextTypeId}",
                deleted, issueId, textTypeId);
        }

        return deleted;
    }

    /// <inheritdoc />
    public async Task<int> InvalidateByRepositoriesAsync(IEnumerable<int> repositoryIds, CancellationToken ct = default)
    {
        var repoIds = repositoryIds.ToList();

        var deleted = await _context.CachedTexts
            .Where(t => repoIds.Contains(t.Issue.RepositoryId))
            .ExecuteDeleteAsync(ct);

        _logger.LogInformation(
            "[TranslationCache] Admin cleared {Count} cache entries for {RepoCount} repositories",
            deleted, repoIds.Count);

        return deleted;
    }

    /// <inheritdoc />
    public async Task<int> InvalidateAllAsync(CancellationToken ct = default)
    {
        var deleted = await _context.CachedTexts
            .ExecuteDeleteAsync(ct);

        _logger.LogWarning(
            "[TranslationCache] Admin cleared ENTIRE translation cache: {Count} entries",
            deleted);

        return deleted;
    }

    /// <inheritdoc />
    public async Task<CacheStatistics> GetStatisticsAsync(CancellationToken ct = default)
    {
        var total = await _context.CachedTexts.CountAsync(ct);

        var byLanguage = await _context.CachedTexts
            .GroupBy(t => t.Language.CultureName)
            .Select(g => new { Language = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Language, x => x.Count, ct);

        var byTextType = await _context.CachedTexts
            .GroupBy(t => t.TextType.Name)
            .Select(g => new { TextType = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.TextType, x => x.Count, ct);

        return new CacheStatistics
        {
            TotalRecords = total,
            ByLanguage = byLanguage,
            ByTextType = byTextType
        };
    }
}
