using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Olbrasoft.GitHub.Issues.Data.Entities;
using Olbrasoft.GitHub.Issues.Data.Repositories;

namespace Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.Repositories;

/// <summary>
/// Entity Framework Core implementation of ICachedTextRepository.
/// </summary>
public class EfCoreCachedTextRepository : EfCoreRepositoryBase, ICachedTextRepository
{
    public EfCoreCachedTextRepository(GitHubDbContext context, ILogger<EfCoreCachedTextRepository> logger)
        : base(context, logger)
    {
    }

    public async Task<CachedText?> GetByIssueAsync(
        int issueId,
        int languageId,
        int textTypeId,
        CancellationToken cancellationToken = default)
    {
        return await GetCachedTextInternalAsync(issueId, languageId, textTypeId, cancellationToken);
    }

    public async Task SaveAsync(CachedText cachedText, CancellationToken cancellationToken = default)
    {
        await SaveCachedTextInternalAsync(cachedText, cancellationToken);
    }

    public async Task DeleteAsync(CachedText cachedText, CancellationToken cancellationToken = default)
    {
        await DeleteCachedTextInternalAsync(cachedText, cancellationToken);
    }

    public async Task<List<CachedText>> GetMultipleCachedTextsAsync(
        IEnumerable<int> issueIds,
        int languageId,
        IEnumerable<int> textTypeIds,
        CancellationToken cancellationToken = default)
    {
        var ids = issueIds.ToList();
        var typeIds = textTypeIds.ToList();

        return await Context.CachedTexts
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
        return await Context.Issues
            .AsNoTracking()
            .Where(i => ids.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id, i => i, cancellationToken);
    }

    public async Task SaveOrUpdateAsync(CachedText cachedText, CancellationToken cancellationToken = default)
    {
        try
        {
            // Check if entity is already being tracked
            var existingTracked = Context.ChangeTracker.Entries<CachedText>()
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
                var existingInDb = await Context.CachedTexts
                    .FirstOrDefaultAsync(c =>
                        c.IssueId == cachedText.IssueId &&
                        c.LanguageId == cachedText.LanguageId &&
                        c.TextTypeId == cachedText.TextTypeId, cancellationToken);

                if (existingInDb != null)
                {
                    // Update existing
                    existingInDb.Content = cachedText.Content;
                    existingInDb.CachedAt = cachedText.CachedAt;
                    Context.Entry(existingInDb).State = EntityState.Modified;
                }
                else
                {
                    // Add new
                    Context.CachedTexts.Add(cachedText);
                }
            }

            await Context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsDuplicateKeyException(ex))
        {
            // Concurrent insert - another thread/request already cached this text
            Logger.LogDebug(
                "[Cache] Concurrent cache insert detected for Issue {IssueId}, Language {LanguageId}, TextType {TextTypeId}",
                cachedText.IssueId,
                cachedText.LanguageId,
                cachedText.TextTypeId);
        }
    }

    public async Task<int> InvalidateByIssueAsync(int issueId, CancellationToken cancellationToken = default)
    {
        return await Context.CachedTexts
            .Where(t => t.IssueId == issueId)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task<int> InvalidateByIssueAndTextTypeAsync(int issueId, int textTypeId, CancellationToken cancellationToken = default)
    {
        return await Context.CachedTexts
            .Where(t => t.IssueId == issueId && t.TextTypeId == textTypeId)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task<int> InvalidateByRepositoriesAsync(IEnumerable<int> repositoryIds, CancellationToken cancellationToken = default)
    {
        var repoIds = repositoryIds.ToList();

        return await Context.CachedTexts
            .Where(t => repoIds.Contains(t.Issue.RepositoryId))
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task<int> InvalidateAllAsync(CancellationToken cancellationToken = default)
    {
        return await Context.CachedTexts
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task<Data.CacheStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default)
    {
        var total = await Context.CachedTexts.CountAsync(cancellationToken);

        var byLanguage = await Context.CachedTexts
            .GroupBy(t => t.Language.CultureName)
            .Select(g => new { Language = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Language, x => x.Count, cancellationToken);

        var byTextType = await Context.CachedTexts
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
        return await Context.Issues.FindAsync(new object[] { issueId }, cancellationToken);
    }

    public async Task<string?> GetIfFreshAsync(
        int issueId,
        int languageId,
        int textTypeId,
        DateTime issueUpdatedAt,
        CancellationToken cancellationToken = default)
    {
        return await GetIfFreshInternalAsync(issueId, languageId, textTypeId, issueUpdatedAt, cancellationToken);
    }
}
