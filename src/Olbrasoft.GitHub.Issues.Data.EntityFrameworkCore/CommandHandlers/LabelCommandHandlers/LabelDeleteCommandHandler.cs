using Microsoft.EntityFrameworkCore;
using Olbrasoft.GitHub.Issues.Data.Commands.LabelCommands;
using Olbrasoft.GitHub.Issues.Data.Entities;

namespace Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.CommandHandlers.LabelCommandHandlers;

/// <summary>
/// Handles command to delete a label from a repository.
/// Also removes all issue-label associations.
/// </summary>
public class LabelDeleteCommandHandler
    : GitHubDbCommandHandler<Label, LabelDeleteCommand, bool>
{
    public LabelDeleteCommandHandler(GitHubDbContext context) : base(context)
    {
        ArgumentNullException.ThrowIfNull(context);
    }

    protected override async Task<bool> ExecuteCommandAsync(
        LabelDeleteCommand command, CancellationToken token)
    {
        var label = await Entities
            .FirstOrDefaultAsync(l => l.RepositoryId == command.RepositoryId
                                   && l.Name == command.Name, token);

        if (label == null)
        {
            return false;
        }

        // Remove all issue-label associations first (cascade delete may not be configured)
        await Context.IssueLabels
            .Where(il => il.LabelId == label.Id)
            .ExecuteDeleteAsync(token);

        // Remove the label
        Entities.Remove(label);
        await SaveChangesAsync(token);

        return true;
    }
}
