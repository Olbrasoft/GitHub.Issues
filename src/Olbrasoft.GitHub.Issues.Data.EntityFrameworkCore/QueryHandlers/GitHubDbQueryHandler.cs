using Olbrasoft.Data.Cqrs;
using Olbrasoft.Data.Cqrs.EntityFrameworkCore;

namespace Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.QueryHandlers;

/// <summary>
/// Base class for query handlers operating on GitHubDbContext.
/// </summary>
/// <typeparam name="TEntity">The entity type being queried.</typeparam>
/// <typeparam name="TQuery">The query type.</typeparam>
/// <typeparam name="TResult">The result type.</typeparam>
public abstract class GitHubDbQueryHandler<TEntity, TQuery, TResult>
    : DbQueryHandler<GitHubDbContext, TEntity, TQuery, TResult>
    where TQuery : BaseQuery<TResult>
    where TEntity : class
{
    protected GitHubDbQueryHandler(GitHubDbContext context) : base(context)
    {
    }

    public override Task<TResult> HandleAsync(TQuery query, CancellationToken token)
    {
        ThrowIfQueryIsNullOrCancellationRequested(query, token);
        return GetResultToHandleAsync(query, token);
    }

    /// <summary>
    /// Override this method to implement the query logic.
    /// </summary>
    protected abstract Task<TResult> GetResultToHandleAsync(TQuery query, CancellationToken token);
}
