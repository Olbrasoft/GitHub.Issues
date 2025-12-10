using Microsoft.EntityFrameworkCore;
using Olbrasoft.GitHub.Issues.Data.Entities;
using Olbrasoft.GitHub.Issues.Data.Queries.RepositoryQueries;

namespace Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.QueryHandlers.RepositoryQueryHandlers;

/// <summary>
/// Handles query to get a repository by its full name.
/// </summary>
public class RepositoryByFullNameQueryHandler
    : GitHubDbQueryHandler<Repository, RepositoryByFullNameQuery, Repository?>
{
    public RepositoryByFullNameQueryHandler(GitHubDbContext context) : base(context)
    {
    }

    protected override async Task<Repository?> GetResultToHandleAsync(
        RepositoryByFullNameQuery query, CancellationToken token)
    {
        return await Entities
            .FirstOrDefaultAsync(r => r.FullName == query.FullName, token);
    }
}
