using Microsoft.EntityFrameworkCore;
using Olbrasoft.GitHub.Issues.Data.Dtos;
using Olbrasoft.GitHub.Issues.Data.Entities;
using Olbrasoft.GitHub.Issues.Data.Queries.IssueQueries;

namespace Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.QueryHandlers.IssueQueryHandlers;

/// <summary>
/// Handles queries for finding issues by their issue numbers (exact match).
/// Used for hybrid search when user enters issue number patterns like #123.
/// </summary>
public class IssuesByNumbersQueryHandler : GitHubDbQueryHandler<Issue, IssuesByNumbersQuery, List<IssueSearchResultDto>>
{
    public IssuesByNumbersQueryHandler(GitHubDbContext context) : base(context)
    {
    }

    protected override async Task<List<IssueSearchResultDto>> GetResultToHandleAsync(
        IssuesByNumbersQuery query, CancellationToken token)
    {
        if (query.IssueNumbers.Count == 0)
        {
            return new List<IssueSearchResultDto>();
        }

        var baseQuery = Entities
            .Include(i => i.Repository)
            .Where(i => query.IssueNumbers.Contains(i.Number));

        // Filter by repository IDs if specified
        if (query.RepositoryIds is { Count: > 0 })
        {
            baseQuery = baseQuery.Where(i => query.RepositoryIds.Contains(i.RepositoryId));
        }

        // Filter by repository name if specified (partial match)
        if (!string.IsNullOrWhiteSpace(query.RepositoryName))
        {
            var repoLower = query.RepositoryName.ToLower();
            baseQuery = baseQuery.Where(i => i.Repository.FullName.ToLower().Contains(repoLower));
        }

        // Apply state filter
        if (string.Equals(query.State, "open", StringComparison.OrdinalIgnoreCase))
        {
            baseQuery = baseQuery.Where(i => i.IsOpen);
        }
        else if (string.Equals(query.State, "closed", StringComparison.OrdinalIgnoreCase))
        {
            baseQuery = baseQuery.Where(i => !i.IsOpen);
        }

        return await baseQuery
            .Select(i => new IssueSearchResultDto
            {
                Id = i.Id,
                IssueNumber = i.Number,
                Title = i.Title,
                IsOpen = i.IsOpen,
                Url = i.Url,
                RepositoryFullName = i.Repository.FullName,
                Similarity = 1.0 // Exact match = 100% similarity
            })
            .ToListAsync(token);
    }
}
