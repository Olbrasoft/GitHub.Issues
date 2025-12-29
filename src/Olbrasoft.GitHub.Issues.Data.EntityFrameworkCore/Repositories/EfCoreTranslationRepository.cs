using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Olbrasoft.GitHub.Issues.Data.Entities;
using Olbrasoft.GitHub.Issues.Data.Repositories;

namespace Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.Repositories;

/// <summary>
/// Entity Framework Core implementation of ITranslationRepository.
/// </summary>
public class EfCoreTranslationRepository : EfCoreRepositoryBase, ITranslationRepository
{
    public EfCoreTranslationRepository(GitHubDbContext context, ILogger<EfCoreTranslationRepository> logger)
        : base(context, logger)
    {
    }

    public async Task<Issue?> GetIssueByIdAsync(int issueId, CancellationToken cancellationToken = default)
    {
        return await Context.Issues.FindAsync(new object[] { issueId }, cancellationToken);
    }

    public async Task<CachedText?> GetCachedTranslationAsync(
        int issueId,
        int languageId,
        int textTypeId,
        CancellationToken cancellationToken = default)
    {
        return await GetCachedTextInternalAsync(issueId, languageId, textTypeId, cancellationToken);
    }

    public async Task DeleteCachedTextAsync(CachedText cachedText, CancellationToken cancellationToken = default)
    {
        await DeleteCachedTextInternalAsync(cachedText, cancellationToken);
    }

    public async Task SaveCachedTextAsync(CachedText cachedText, CancellationToken cancellationToken = default)
    {
        await SaveCachedTextInternalAsync(cachedText, cancellationToken);
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
