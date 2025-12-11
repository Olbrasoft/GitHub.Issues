using Microsoft.EntityFrameworkCore;
using Olbrasoft.GitHub.Issues.Data.Dtos;
using Olbrasoft.GitHub.Issues.Data.Entities;
using Olbrasoft.GitHub.Issues.Data.Queries.RepositoryQueries;

namespace Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.QueryHandlers.RepositoryQueryHandlers;

/// <summary>
/// Handles repository sync status queries for the intelligent sync UI.
/// Returns all repositories with their issue counts and last sync timestamps.
/// </summary>
public class RepositoriesSyncStatusQueryHandler
    : GitHubDbQueryHandler<Repository, RepositoriesSyncStatusQuery, IEnumerable<RepositorySyncStatusDto>>
{
    public RepositoriesSyncStatusQueryHandler(GitHubDbContext context) : base(context)
    {
    }

    protected override async Task<IEnumerable<RepositorySyncStatusDto>> GetResultToHandleAsync(
        RepositoriesSyncStatusQuery query, CancellationToken token)
    {
        return await Entities
            .OrderBy(r => r.FullName)
            .Select(r => new RepositorySyncStatusDto
            {
                Id = r.Id,
                FullName = r.FullName,
                IssueCount = r.Issues.Count,
                LastSyncedAt = r.LastSyncedAt
            })
            .ToListAsync(token);
    }
}
