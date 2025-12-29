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

    public async Task<Issue?> GetIssueByIdAsync(int issueId, CancellationToken cancellationToken = default)
    {
        return await _context.Issues.FindAsync(new object[] { issueId }, cancellationToken);
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
