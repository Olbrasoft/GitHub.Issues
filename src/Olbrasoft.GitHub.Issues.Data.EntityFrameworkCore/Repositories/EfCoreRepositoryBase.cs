using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Olbrasoft.GitHub.Issues.Data.Entities;

namespace Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.Repositories;

/// <summary>
/// Base class for EF Core repositories with shared cache functionality.
/// Provides common methods for duplicate key detection, cache validation, and logging.
/// </summary>
public abstract class EfCoreRepositoryBase
{
    protected readonly GitHubDbContext Context;
    protected readonly ILogger Logger;

    protected EfCoreRepositoryBase(GitHubDbContext context, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(logger);

        Context = context;
        Logger = logger;
    }

    /// <summary>
    /// Gets cached text by issue ID, language ID, and text type ID.
    /// </summary>
    protected async Task<CachedText?> GetCachedTextInternalAsync(
        int issueId,
        int languageId,
        int textTypeId,
        CancellationToken cancellationToken = default)
    {
        return await Context.CachedTexts
            .FirstOrDefaultAsync(t =>
                t.IssueId == issueId &&
                t.LanguageId == languageId &&
                t.TextTypeId == textTypeId, cancellationToken);
    }

    /// <summary>
    /// Deletes cached text from the database.
    /// </summary>
    protected async Task DeleteCachedTextInternalAsync(CachedText cachedText, CancellationToken cancellationToken = default)
    {
        Context.CachedTexts.Remove(cachedText);
        await Context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Gets cached text if it's still fresh (issue hasn't been updated after cache was created).
    /// Automatically deletes stale cache with logging.
    /// </summary>
    protected async Task<string?> GetIfFreshInternalAsync(
        int issueId,
        int languageId,
        int textTypeId,
        DateTime issueUpdatedAt,
        CancellationToken cancellationToken = default)
    {
        var cached = await GetCachedTextInternalAsync(issueId, languageId, textTypeId, cancellationToken);

        if (cached == null) return null;

        // Validate freshness - if issue was updated after cache, invalidate
        if (issueUpdatedAt > cached.CachedAt)
        {
            // Cache is stale - delete it with logging
            Logger.LogDebug(
                "[Cache] Cache STALE for issue {IssueId}, language {LanguageId}, textType {TextTypeId} - issue updated {IssueUpdated}, cache created {CacheCreated}, deleting",
                issueId,
                languageId,
                textTypeId,
                issueUpdatedAt,
                cached.CachedAt);

            await DeleteCachedTextInternalAsync(cached, cancellationToken);
            return null;
        }

        // Cache is fresh
        return cached.Content;
    }

    /// <summary>
    /// Saves cached text with duplicate key exception handling and logging.
    /// </summary>
    protected async Task SaveCachedTextInternalAsync(CachedText cachedText, CancellationToken cancellationToken = default)
    {
        try
        {
            Context.CachedTexts.Add(cachedText);
            await Context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsDuplicateKeyException(ex))
        {
            // Concurrent insert - another request already cached this text
            Logger.LogDebug(
                "[Cache] Concurrent cache insert detected for Issue {IssueId}, Language {LanguageId}, TextType {TextTypeId}",
                cachedText.IssueId,
                cachedText.LanguageId,
                cachedText.TextTypeId);
        }
    }

    /// <summary>
    /// Checks if exception is a duplicate key violation.
    /// Supports PostgreSQL (23505), SQL Server (2627, 2601).
    /// </summary>
    protected static bool IsDuplicateKeyException(DbUpdateException ex)
    {
        var message = ex.InnerException?.Message ?? string.Empty;
        return message.Contains("23505") || // PostgreSQL unique violation
               message.Contains("2627") ||  // SQL Server unique constraint
               message.Contains("2601") ||  // SQL Server unique index
               message.Contains("duplicate key") ||
               message.Contains("unique constraint");
    }
}
