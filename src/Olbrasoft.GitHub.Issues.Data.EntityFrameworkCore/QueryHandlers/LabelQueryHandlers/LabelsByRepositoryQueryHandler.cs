using Microsoft.EntityFrameworkCore;
using Olbrasoft.GitHub.Issues.Data.Entities;
using Olbrasoft.GitHub.Issues.Data.Queries.LabelQueries;

namespace Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.QueryHandlers.LabelQueryHandlers;

/// <summary>
/// Handles query to get all labels for a repository.
/// </summary>
public class LabelsByRepositoryQueryHandler
    : GitHubDbQueryHandler<Label, LabelsByRepositoryQuery, List<Label>>
{
    public LabelsByRepositoryQueryHandler(GitHubDbContext context) : base(context)
    {
    }

    protected override async Task<List<Label>> GetResultToHandleAsync(
        LabelsByRepositoryQuery query, CancellationToken token)
    {
        return await Entities
            .Where(l => l.RepositoryId == query.RepositoryId)
            .ToListAsync(token);
    }
}
