using Microsoft.EntityFrameworkCore;
using Olbrasoft.GitHub.Issues.Data.Entities;
using Olbrasoft.GitHub.Issues.Data.Queries.LabelQueries;

namespace Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.QueryHandlers.LabelQueryHandlers;

/// <summary>
/// Handles query to get a single label by repository ID and name.
/// </summary>
public class LabelByRepositoryAndNameQueryHandler
    : GitHubDbQueryHandler<Label, LabelByRepositoryAndNameQuery, Label?>
{
    public LabelByRepositoryAndNameQueryHandler(GitHubDbContext context) : base(context)
    {
    }

    protected override async Task<Label?> GetResultToHandleAsync(
        LabelByRepositoryAndNameQuery query, CancellationToken token)
    {
        return await Entities
            .FirstOrDefaultAsync(l => l.RepositoryId == query.RepositoryId
                                   && l.Name == query.Name, token);
    }
}
