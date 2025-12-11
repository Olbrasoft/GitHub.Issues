using Microsoft.EntityFrameworkCore;
using Olbrasoft.GitHub.Issues.Data.Dtos;
using Olbrasoft.GitHub.Issues.Data.Entities;
using Olbrasoft.GitHub.Issues.Data.Queries.IssueQueries;

namespace Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.QueryHandlers.IssueQueryHandlers;

/// <summary>
/// Handles issue list queries without vector search (for browsing repos).
/// Returns issues ordered by issue number descending.
/// </summary>
public class IssueListQueryHandler : GitHubDbQueryHandler<Issue, IssueListQuery, IssueSearchPageDto>
{
    public IssueListQueryHandler(GitHubDbContext context) : base(context)
    {
    }

    protected override async Task<IssueSearchPageDto> GetResultToHandleAsync(
        IssueListQuery query, CancellationToken token)
    {
        if (query.RepositoryIds.Count == 0)
        {
            return new IssueSearchPageDto();
        }

        var baseQuery = BuildBaseQuery(query.State, query.RepositoryIds);

        var totalCount = await baseQuery.CountAsync(token);
        var skip = (query.Page - 1) * query.PageSize;

        var results = await baseQuery
            .OrderByDescending(i => i.Number)
            .Skip(skip)
            .Take(query.PageSize)
            .Select(i => new IssueSearchResultDto
            {
                Id = i.Id,
                IssueNumber = i.Number,
                Title = i.Title,
                IsOpen = i.IsOpen,
                Url = i.Url,
                RepositoryFullName = i.Repository.FullName,
                Similarity = 1.0f, // No similarity for list queries
                Labels = i.IssueLabels.Select(il => new LabelDto(il.Label.Name, il.Label.Color)).ToList()
            })
            .ToListAsync(token);

        return new IssueSearchPageDto
        {
            Results = results,
            TotalCount = totalCount,
            Page = query.Page,
            PageSize = query.PageSize,
            TotalPages = (int)Math.Ceiling((double)totalCount / query.PageSize)
        };
    }

    private IQueryable<Issue> BuildBaseQuery(string state, IReadOnlyList<int> repositoryIds)
    {
        var query = Entities
            .Include(i => i.Repository)
            .Where(i => repositoryIds.Contains(i.RepositoryId));

        if (string.Equals(state, "open", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(i => i.IsOpen);
        }
        else if (string.Equals(state, "closed", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(i => !i.IsOpen);
        }

        return query;
    }
}
