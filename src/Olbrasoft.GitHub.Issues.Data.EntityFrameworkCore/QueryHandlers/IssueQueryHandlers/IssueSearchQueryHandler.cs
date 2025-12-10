using Microsoft.EntityFrameworkCore;
using Olbrasoft.GitHub.Issues.Data.Dtos;
using Olbrasoft.GitHub.Issues.Data.Entities;
using Olbrasoft.GitHub.Issues.Data.Queries.IssueQueries;
using Pgvector.EntityFrameworkCore;

namespace Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.QueryHandlers.IssueQueryHandlers;

/// <summary>
/// Handles issue search queries using vector similarity.
/// PostgreSQL implementation using pgvector CosineDistance().
/// </summary>
public class IssueSearchQueryHandler : GitHubDbQueryHandler<Issue, IssueSearchQuery, IssueSearchPageDto>
{
    public IssueSearchQueryHandler(GitHubDbContext context) : base(context)
    {
    }

    protected override async Task<IssueSearchPageDto> GetResultToHandleAsync(
        IssueSearchQuery query, CancellationToken token)
    {
        var baseQuery = BuildBaseQuery(query.State, query.RepositoryIds);

        // Get total count for pagination
        var totalCount = await baseQuery.CountAsync(token);

        // Calculate skip and take
        var skip = (query.Page - 1) * query.PageSize;

        // Execute vector similarity search
        var results = await baseQuery
            .OrderBy(i => i.Embedding!.CosineDistance(query.QueryEmbedding))
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
                Similarity = 1 - i.Embedding!.CosineDistance(query.QueryEmbedding)
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

    private IQueryable<Issue> BuildBaseQuery(string state, IReadOnlyList<int>? repositoryIds)
    {
        var query = Entities
            .Include(i => i.Repository)
            .Where(i => i.Embedding != null)
            .AsQueryable();

        if (string.Equals(state, "open", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(i => i.IsOpen);
        }
        else if (string.Equals(state, "closed", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(i => !i.IsOpen);
        }

        // Filter by repository IDs if provided
        if (repositoryIds is { Count: > 0 })
        {
            query = query.Where(i => repositoryIds.Contains(i.RepositoryId));
        }

        return query;
    }
}
