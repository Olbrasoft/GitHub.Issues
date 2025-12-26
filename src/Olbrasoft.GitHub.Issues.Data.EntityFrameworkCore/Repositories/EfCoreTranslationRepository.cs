using Microsoft.EntityFrameworkCore;
using Olbrasoft.GitHub.Issues.Data.Entities;
using Olbrasoft.GitHub.Issues.Data.Repositories;

namespace Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.Repositories;

/// <summary>
/// Entity Framework Core implementation of ITranslationRepository.
/// </summary>
public class EfCoreTranslationRepository : ITranslationRepository
{
    private readonly GitHubDbContext _context;

    public EfCoreTranslationRepository(GitHubDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        _context = context;
    }

    public async Task<Issue?> GetIssueByIdAsync(int issueId, CancellationToken cancellationToken = default)
    {
        return await _context.Issues.FindAsync(new object[] { issueId }, cancellationToken);
    }

    public async Task<CachedText?> GetCachedTranslationAsync(
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

    public async Task DeleteCachedTextAsync(CachedText cachedText, CancellationToken cancellationToken = default)
    {
        _context.CachedTexts.Remove(cachedText);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task SaveCachedTextAsync(CachedText cachedText, CancellationToken cancellationToken = default)
    {
        _context.CachedTexts.Add(cachedText);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
