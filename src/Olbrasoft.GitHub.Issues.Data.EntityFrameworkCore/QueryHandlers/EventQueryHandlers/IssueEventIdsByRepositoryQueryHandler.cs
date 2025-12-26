using Microsoft.EntityFrameworkCore;
using Olbrasoft.GitHub.Issues.Data.Entities;
using Olbrasoft.GitHub.Issues.Data.Queries.EventQueries;

namespace Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.QueryHandlers.EventQueryHandlers;

/// <summary>
/// Handles query to get all existing GitHub event IDs for a repository.
/// </summary>
public class IssueEventIdsByRepositoryQueryHandler
    : GitHubDbQueryHandler<IssueEvent, IssueEventIdsByRepositoryQuery, HashSet<long>>
{
    public IssueEventIdsByRepositoryQueryHandler(GitHubDbContext context) : base(context)
    {
        ArgumentNullException.ThrowIfNull(context);
    }

    protected override async Task<HashSet<long>> GetResultToHandleAsync(
        IssueEventIdsByRepositoryQuery query, CancellationToken token)
    {
        var eventIds = await Entities
            .Where(e => e.Issue.RepositoryId == query.RepositoryId)
            .Select(e => e.GitHubEventId)
            .ToListAsync(token);

        return new HashSet<long>(eventIds);
    }
}
