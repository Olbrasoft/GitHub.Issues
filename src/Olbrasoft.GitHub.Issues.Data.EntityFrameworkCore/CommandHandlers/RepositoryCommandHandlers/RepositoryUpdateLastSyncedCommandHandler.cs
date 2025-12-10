using Microsoft.EntityFrameworkCore;
using Olbrasoft.GitHub.Issues.Data.Commands.RepositoryCommands;
using Olbrasoft.GitHub.Issues.Data.Entities;

namespace Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.CommandHandlers.RepositoryCommandHandlers;

/// <summary>
/// Handles command to update LastSyncedAt timestamp for a repository.
/// </summary>
public class RepositoryUpdateLastSyncedCommandHandler
    : GitHubDbCommandHandler<Repository, RepositoryUpdateLastSyncedCommand, bool>
{
    public RepositoryUpdateLastSyncedCommandHandler(GitHubDbContext context) : base(context)
    {
    }

    protected override async Task<bool> ExecuteCommandAsync(
        RepositoryUpdateLastSyncedCommand command, CancellationToken token)
    {
        var repository = await Entities.FindAsync(new object[] { command.RepositoryId }, token);

        if (repository == null)
        {
            return false;
        }

        repository.LastSyncedAt = command.LastSyncedAt;
        await SaveChangesAsync(token);
        return true;
    }
}
