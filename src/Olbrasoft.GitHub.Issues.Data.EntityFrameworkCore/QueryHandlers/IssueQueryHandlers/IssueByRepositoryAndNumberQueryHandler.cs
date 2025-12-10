using Microsoft.EntityFrameworkCore;
using Olbrasoft.GitHub.Issues.Data.Entities;
using Olbrasoft.GitHub.Issues.Data.Queries.IssueQueries;

namespace Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.QueryHandlers.IssueQueryHandlers;

/// <summary>
/// Handles query to get a single issue by repository ID and issue number.
/// </summary>
public class IssueByRepositoryAndNumberQueryHandler
    : GitHubDbQueryHandler<Issue, IssueByRepositoryAndNumberQuery, Issue?>
{
    public IssueByRepositoryAndNumberQueryHandler(GitHubDbContext context) : base(context)
    {
    }

    protected override async Task<Issue?> GetResultToHandleAsync(
        IssueByRepositoryAndNumberQuery query, CancellationToken token)
    {
        return await Entities
            .Include(i => i.IssueLabels)
            .ThenInclude(il => il.Label)
            .FirstOrDefaultAsync(i => i.RepositoryId == query.RepositoryId
                                   && i.Number == query.Number, token);
    }
}
