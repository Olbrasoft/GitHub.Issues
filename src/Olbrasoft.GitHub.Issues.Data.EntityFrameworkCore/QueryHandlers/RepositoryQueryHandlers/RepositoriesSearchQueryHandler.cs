using Microsoft.EntityFrameworkCore;
using Olbrasoft.GitHub.Issues.Data.Dtos;
using Olbrasoft.GitHub.Issues.Data.Entities;
using Olbrasoft.GitHub.Issues.Data.Queries.RepositoryQueries;

namespace Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.QueryHandlers.RepositoryQueryHandlers;

/// <summary>
/// Handles repository search queries by name pattern.
/// </summary>
public class RepositoriesSearchQueryHandler
    : GitHubDbQueryHandler<Repository, RepositoriesSearchQuery, IEnumerable<RepositorySearchResultDto>>
{
    public RepositoriesSearchQueryHandler(GitHubDbContext context) : base(context)
    {
    }

    protected override async Task<IEnumerable<RepositorySearchResultDto>> GetResultToHandleAsync(
        RepositoriesSearchQuery query, CancellationToken token)
    {
        var term = query.Term?.Trim() ?? string.Empty;

        if (string.IsNullOrEmpty(term))
        {
            return Enumerable.Empty<RepositorySearchResultDto>();
        }

        return await Entities
            .Where(r => EF.Functions.ILike(r.FullName, $"%{term}%"))
            .OrderBy(r => r.FullName)
            .Take(query.MaxResults)
            .Select(r => new RepositorySearchResultDto
            {
                Id = r.Id,
                FullName = r.FullName
            })
            .ToListAsync(token);
    }
}
