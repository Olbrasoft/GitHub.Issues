using Microsoft.EntityFrameworkCore;
using Olbrasoft.GitHub.Issues.Data.Entities;
using Olbrasoft.GitHub.Issues.Data.Queries.IssueQueries;

namespace Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.QueryHandlers.IssueQueryHandlers;

/// <summary>
/// Handles query to get all issues for a repository as a dictionary keyed by issue number.
/// </summary>
public class IssuesByRepositoryQueryHandler
    : GitHubDbQueryHandler<Issue, IssuesByRepositoryQuery, Dictionary<int, Issue>>
{
    public IssuesByRepositoryQueryHandler(GitHubDbContext context) : base(context)
    {
        ArgumentNullException.ThrowIfNull(context);
    }

    protected override async Task<Dictionary<int, Issue>> GetResultToHandleAsync(
        IssuesByRepositoryQuery query, CancellationToken token)
    {
        return await Entities
            .Where(i => i.RepositoryId == query.RepositoryId)
            .ToDictionaryAsync(i => i.Number, token);
    }
}
