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
        ArgumentNullException.ThrowIfNull(context);
    }

    protected override async Task<IEnumerable<RepositorySearchResultDto>> GetResultToHandleAsync(
        RepositoriesSearchQuery query, CancellationToken token)
    {
        var term = query.Term?.Trim() ?? string.Empty;

        if (string.IsNullOrEmpty(term))
        {
            return Enumerable.Empty<RepositorySearchResultDto>();
        }

        // Use portable case-insensitive search (works with both PostgreSQL and SQL Server)
        var termLower = term.ToLower();
        return await Entities
            .Where(r => r.FullName.ToLower().Contains(termLower))
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
