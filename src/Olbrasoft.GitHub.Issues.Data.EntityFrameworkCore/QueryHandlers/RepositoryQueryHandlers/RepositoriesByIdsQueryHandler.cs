using Microsoft.EntityFrameworkCore;
using Olbrasoft.GitHub.Issues.Data.Dtos;
using Olbrasoft.GitHub.Issues.Data.Entities;
using Olbrasoft.GitHub.Issues.Data.Queries.RepositoryQueries;

namespace Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.QueryHandlers.RepositoryQueryHandlers;

/// <summary>
/// Handles query to get repositories by their IDs.
/// </summary>
public class RepositoriesByIdsQueryHandler
    : GitHubDbQueryHandler<Repository, RepositoriesByIdsQuery, IEnumerable<RepositorySearchResultDto>>
{
    public RepositoriesByIdsQueryHandler(GitHubDbContext context) : base(context)
    {
    }

    protected override async Task<IEnumerable<RepositorySearchResultDto>> GetResultToHandleAsync(
        RepositoriesByIdsQuery query, CancellationToken token)
    {
        if (query.Ids == null || query.Ids.Count == 0)
        {
            return Enumerable.Empty<RepositorySearchResultDto>();
        }

        return await Entities
            .Where(r => query.Ids.Contains(r.Id))
            .Select(r => new RepositorySearchResultDto
            {
                Id = r.Id,
                FullName = r.FullName
            })
            .ToListAsync(token);
    }
}
