using Olbrasoft.Data.Cqrs;
using Olbrasoft.Data.Cqrs.EntityFrameworkCore;

namespace Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.CommandHandlers;

/// <summary>
/// Base class for command handlers operating on GitHubDbContext.
/// </summary>
/// <typeparam name="TEntity">The entity type being modified.</typeparam>
/// <typeparam name="TCommand">The command type.</typeparam>
/// <typeparam name="TResult">The result type.</typeparam>
public abstract class GitHubDbCommandHandler<TEntity, TCommand, TResult>
    : DbCommandHandler<GitHubDbContext, TEntity, TCommand, TResult>, ICommandHandler<TCommand, TResult>
    where TCommand : BaseCommand<TResult>
    where TEntity : class
{
    protected GitHubDbCommandHandler(GitHubDbContext context) : base(context)
    {
    }

    public override Task<TResult> HandleAsync(TCommand command, CancellationToken token)
    {
        ThrowIfCommandIsNullOrCancellationRequested(command, token);
        return ExecuteCommandAsync(command, token);
    }

    /// <summary>
    /// Override this method to implement the command logic.
    /// </summary>
    protected abstract Task<TResult> ExecuteCommandAsync(TCommand command, CancellationToken token);
}
