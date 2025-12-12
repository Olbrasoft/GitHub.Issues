using Microsoft.EntityFrameworkCore;
using Olbrasoft.GitHub.Issues.Data.Dtos;
using Olbrasoft.GitHub.Issues.Data.Entities;
using Olbrasoft.GitHub.Issues.Data.Queries.IssueQueries;

namespace Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.QueryHandlers.IssueQueryHandlers;

/// <summary>
/// Handles text-based search queries using ILIKE pattern matching.
/// Fallback when semantic/vector search is unavailable.
/// </summary>
public class IssueTextSearchQueryHandler : GitHubDbQueryHandler<Issue, IssueTextSearchQuery, IssueSearchPageDto>
{
    public IssueTextSearchQueryHandler(GitHubDbContext context) : base(context)
    {
    }

    protected override async Task<IssueSearchPageDto> GetResultToHandleAsync(
        IssueTextSearchQuery query, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(query.SearchText))
        {
            return new IssueSearchPageDto();
        }

        var baseQuery = BuildBaseQuery(query.SearchText, query.State, query.RepositoryIds);

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
                Similarity = 0.5f, // Fixed similarity for text search (lower than semantic)
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

    private IQueryable<Issue> BuildBaseQuery(string searchText, string state, IReadOnlyList<int>? repositoryIds)
    {
        var pattern = $"%{searchText}%";

        // Search only in Title - issue body is fetched from GitHub on demand
        var query = Entities
            .Include(i => i.Repository)
            .Include(i => i.IssueLabels)
                .ThenInclude(il => il.Label)
            .Where(i => EF.Functions.ILike(i.Title, pattern));

        if (repositoryIds != null && repositoryIds.Count > 0)
        {
            query = query.Where(i => repositoryIds.Contains(i.RepositoryId));
        }

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
