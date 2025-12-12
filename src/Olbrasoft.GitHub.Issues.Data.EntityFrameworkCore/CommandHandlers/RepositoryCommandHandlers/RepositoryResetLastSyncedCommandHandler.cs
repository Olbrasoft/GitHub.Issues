using Microsoft.EntityFrameworkCore;
using Olbrasoft.GitHub.Issues.Data.Commands.RepositoryCommands;
using Olbrasoft.GitHub.Issues.Data.Entities;

namespace Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.CommandHandlers.RepositoryCommandHandlers;

/// <summary>
/// Handles command to reset LastSyncedAt to NULL for a repository.
/// </summary>
public class RepositoryResetLastSyncedCommandHandler
    : GitHubDbCommandHandler<Repository, RepositoryResetLastSyncedCommand, bool>
{
    public RepositoryResetLastSyncedCommandHandler(GitHubDbContext context) : base(context)
    {
    }

    protected override async Task<bool> ExecuteCommandAsync(
        RepositoryResetLastSyncedCommand command, CancellationToken token)
    {
        var repository = await Entities
            .FirstOrDefaultAsync(r => r.FullName == command.FullName, token);

        if (repository == null)
        {
            return false;
        }

        repository.LastSyncedAt = null;
        await SaveChangesAsync(token);
        return true;
    }
}
