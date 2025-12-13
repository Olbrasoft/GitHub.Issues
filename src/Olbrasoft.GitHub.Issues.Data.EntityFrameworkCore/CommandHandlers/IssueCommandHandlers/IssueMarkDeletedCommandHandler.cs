using Microsoft.EntityFrameworkCore;
using Olbrasoft.GitHub.Issues.Data.Commands.IssueCommands;
using Olbrasoft.GitHub.Issues.Data.Entities;

namespace Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.CommandHandlers.IssueCommandHandlers;

/// <summary>
/// Handles command to mark issues as deleted (soft delete).
/// Used during full sync when issues exist in DB but not on GitHub.
/// </summary>
public class IssueMarkDeletedCommandHandler
    : GitHubDbCommandHandler<Issue, IssueMarkDeletedCommand, int>
{
    public IssueMarkDeletedCommandHandler(GitHubDbContext context) : base(context)
    {
    }

    protected override async Task<int> ExecuteCommandAsync(
        IssueMarkDeletedCommand command, CancellationToken token)
    {
        if (command.IssueIds.Count == 0)
        {
            return 0;
        }

        // Mark issues as deleted in batch
        var updatedCount = await Entities
            .Where(i => command.IssueIds.Contains(i.Id) && !i.IsDeleted)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(i => i.IsDeleted, true), token);

        return updatedCount;
    }
}
