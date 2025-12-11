using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Olbrasoft.GitHub.Issues.Data.Dtos;
using Olbrasoft.GitHub.Issues.Data.Entities;
using Olbrasoft.GitHub.Issues.Data.Queries.IssueQueries;
using Pgvector.EntityFrameworkCore;

namespace Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.QueryHandlers.IssueQueryHandlers;

/// <summary>
/// Handles issue search queries using vector similarity.
/// Supports both PostgreSQL (pgvector CosineDistance) and SQL Server (EF.Functions.VectorDistance).
/// </summary>
public class IssueSearchQueryHandler : GitHubDbQueryHandler<Issue, IssueSearchQuery, IssueSearchPageDto>
{
    private readonly DatabaseSettings _settings;

    public IssueSearchQueryHandler(GitHubDbContext context, IOptions<DatabaseSettings> settings) : base(context)
    {
        _settings = settings.Value;
    }

    protected override async Task<IssueSearchPageDto> GetResultToHandleAsync(
        IssueSearchQuery query, CancellationToken token)
    {
        return _settings.Provider switch
        {
            DatabaseProvider.SqlServer => await ExecuteSqlServerSearchAsync(query, token),
            _ => await ExecutePostgreSqlSearchAsync(query, token)
        };
    }

    /// <summary>
    /// PostgreSQL implementation using pgvector CosineDistance().
    /// </summary>
    private async Task<IssueSearchPageDto> ExecutePostgreSqlSearchAsync(
        IssueSearchQuery query, CancellationToken token)
    {
        var baseQuery = BuildBaseQuery(query.State, query.RepositoryIds);

        var totalCount = await baseQuery.CountAsync(token);
        var skip = (query.Page - 1) * query.PageSize;

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
                Similarity = 1 - i.Embedding!.CosineDistance(query.QueryEmbedding),
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

    /// <summary>
    /// SQL Server implementation using EF.Functions.VectorDistance() with native VECTOR type.
    /// </summary>
    private async Task<IssueSearchPageDto> ExecuteSqlServerSearchAsync(
        IssueSearchQuery query, CancellationToken token)
    {
        var baseQuery = BuildBaseQuery(query.State, query.RepositoryIds);

        var totalCount = await baseQuery.CountAsync(token);
        var skip = (query.Page - 1) * query.PageSize;

        var results = await baseQuery
            .OrderBy(i => EF.Functions.VectorDistance("cosine", i.Embedding!, query.QueryEmbedding))
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
                Similarity = 1 - EF.Functions.VectorDistance("cosine", i.Embedding!, query.QueryEmbedding),
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

        if (repositoryIds is { Count: > 0 })
        {
            query = query.Where(i => repositoryIds.Contains(i.RepositoryId));
        }

        return query;
    }
}
