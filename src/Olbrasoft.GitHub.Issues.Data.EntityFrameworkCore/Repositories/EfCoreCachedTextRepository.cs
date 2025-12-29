using Microsoft.EntityFrameworkCore;
using Olbrasoft.GitHub.Issues.Data.Entities;
using Olbrasoft.GitHub.Issues.Data.Repositories;

namespace Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.Repositories;

/// <summary>
/// Entity Framework Core implementation of ICachedTextRepository.
/// </summary>
public class EfCoreCachedTextRepository : ICachedTextRepository
{
    private readonly GitHubDbContext _context;

    public EfCoreCachedTextRepository(GitHubDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        _context = context;
    }

    public async Task<CachedText?> GetByIssueAsync(
        int issueId,
        int languageId,
        int textTypeId,
        CancellationToken cancellationToken = default)
    {
        return await _context.CachedTexts
            .FirstOrDefaultAsync(t =>
                t.IssueId == issueId &&
                t.LanguageId == languageId &&
                t.TextTypeId == textTypeId, cancellationToken);
    }

    public async Task SaveAsync(CachedText cachedText, CancellationToken cancellationToken = default)
    {
        try
        {
            _context.CachedTexts.Add(cachedText);
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsDuplicateKeyException(ex))
        {
            // Concurrent insert - another thread/request already cached this text
            // This is expected behavior, no action needed
        }
    }

    public async Task DeleteAsync(CachedText cachedText, CancellationToken cancellationToken = default)
    {
        _context.CachedTexts.Remove(cachedText);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<CachedText>> GetMultipleCachedTextsAsync(
        IEnumerable<int> issueIds,
        int languageId,
        IEnumerable<int> textTypeIds,
        CancellationToken cancellationToken = default)
    {
        var ids = issueIds.ToList();
        var typeIds = textTypeIds.ToList();

        return await _context.CachedTexts
            .AsNoTracking()
            .Where(t => ids.Contains(t.IssueId) &&
                        t.LanguageId == languageId &&
                        typeIds.Contains(t.TextTypeId))
            .ToListAsync(cancellationToken);
    }

    public async Task<Dictionary<int, Issue>> GetIssuesByIdsAsync(
        IEnumerable<int> issueIds,
        CancellationToken cancellationToken = default)
    {
        var ids = issueIds.ToList();
        return await _context.Issues
            .AsNoTracking()
            .Where(i => ids.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id, i => i, cancellationToken);
    }

    public async Task SaveOrUpdateAsync(CachedText cachedText, CancellationToken cancellationToken = default)
    {
        try
        {
            // Check if entity is already being tracked
            var existingTracked = _context.ChangeTracker.Entries<CachedText>()
                .FirstOrDefault(e =>
                    e.Entity.IssueId == cachedText.IssueId &&
                    e.Entity.LanguageId == cachedText.LanguageId &&
                    e.Entity.TextTypeId == cachedText.TextTypeId);

            if (existingTracked != null)
            {
                // Update tracked entity
                existingTracked.Entity.Content = cachedText.Content;
                existingTracked.Entity.CachedAt = cachedText.CachedAt;
                existingTracked.State = EntityState.Modified;
            }
            else
            {
                // Check if exists in database
                var existingInDb = await _context.CachedTexts
                    .FirstOrDefaultAsync(c =>
                        c.IssueId == cachedText.IssueId &&
                        c.LanguageId == cachedText.LanguageId &&
                        c.TextTypeId == cachedText.TextTypeId, cancellationToken);

                if (existingInDb != null)
                {
                    // Update existing
                    existingInDb.Content = cachedText.Content;
                    existingInDb.CachedAt = cachedText.CachedAt;
                    _context.Entry(existingInDb).State = EntityState.Modified;
                }
                else
                {
                    // Add new
                    _context.CachedTexts.Add(cachedText);
                }
            }

            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsDuplicateKeyException(ex))
        {
            // Concurrent insert - another thread/request already cached this text
            // This is expected behavior, no action needed
        }
    }

    public async Task<int> InvalidateByIssueAsync(int issueId, CancellationToken cancellationToken = default)
    {
        return await _context.CachedTexts
            .Where(t => t.IssueId == issueId)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task<int> InvalidateByIssueAndTextTypeAsync(int issueId, int textTypeId, CancellationToken cancellationToken = default)
    {
        return await _context.CachedTexts
            .Where(t => t.IssueId == issueId && t.TextTypeId == textTypeId)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task<int> InvalidateByRepositoriesAsync(IEnumerable<int> repositoryIds, CancellationToken cancellationToken = default)
    {
        var repoIds = repositoryIds.ToList();

        return await _context.CachedTexts
            .Where(t => repoIds.Contains(t.Issue.RepositoryId))
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task<int> InvalidateAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.CachedTexts
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task<Data.CacheStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default)
    {
        var total = await _context.CachedTexts.CountAsync(cancellationToken);

        var byLanguage = await _context.CachedTexts
            .GroupBy(t => t.Language.CultureName)
            .Select(g => new { Language = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Language, x => x.Count, cancellationToken);

        var byTextType = await _context.CachedTexts
            .GroupBy(t => t.TextType.Name)
            .Select(g => new { TextType = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.TextType, x => x.Count, cancellationToken);

        return new Data.CacheStatistics
        {
            TotalRecords = total,
            ByLanguage = byLanguage,
            ByTextType = byTextType
        };
    }

    public async Task<Issue?> GetIssueByIdAsync(int issueId, CancellationToken cancellationToken = default)
    {
        return await _context.Issues.FindAsync(new object[] { issueId }, cancellationToken);
    }

    public async Task<string?> GetIfFreshAsync(
        int issueId,
        int languageId,
        int textTypeId,
        DateTime issueUpdatedAt,
        CancellationToken cancellationToken = default)
    {
        var cached = await GetByIssueAsync(issueId, languageId, textTypeId, cancellationToken);

        if (cached == null) return null;

        // Validate freshness - if issue was updated after cache, invalidate
        if (issueUpdatedAt > cached.CachedAt)
        {
            // Cache is stale - delete it
            await DeleteAsync(cached, cancellationToken);
            return null;
        }

        // Cache is fresh
        return cached.Content;
    }

    /// <summary>
    /// Checks if exception is a duplicate key violation.
    /// Supports PostgreSQL (23505), SQL Server (2627, 2601).
    /// </summary>
    private static bool IsDuplicateKeyException(DbUpdateException ex)
    {
        var message = ex.InnerException?.Message ?? string.Empty;
        return message.Contains("23505") || // PostgreSQL unique violation
               message.Contains("2627") ||  // SQL Server unique constraint
               message.Contains("2601") ||  // SQL Server unique index
               message.Contains("duplicate key") ||
               message.Contains("unique constraint");
    }
}
