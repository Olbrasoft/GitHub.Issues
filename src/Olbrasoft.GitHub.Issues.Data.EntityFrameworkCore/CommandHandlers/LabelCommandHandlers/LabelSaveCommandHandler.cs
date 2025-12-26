using Microsoft.EntityFrameworkCore;
using Olbrasoft.GitHub.Issues.Data.Commands.LabelCommands;
using Olbrasoft.GitHub.Issues.Data.Entities;

namespace Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.CommandHandlers.LabelCommandHandlers;

/// <summary>
/// Handles command to save (create or update) a label.
/// </summary>
public class LabelSaveCommandHandler
    : GitHubDbCommandHandler<Label, LabelSaveCommand, Label>
{
    public LabelSaveCommandHandler(GitHubDbContext context) : base(context)
    {
        ArgumentNullException.ThrowIfNull(context);
    }

    protected override async Task<Label> ExecuteCommandAsync(
        LabelSaveCommand command, CancellationToken token)
    {
        var label = await Entities
            .FirstOrDefaultAsync(l => l.RepositoryId == command.RepositoryId
                                   && l.Name == command.Name, token);

        if (label == null)
        {
            label = new Label
            {
                RepositoryId = command.RepositoryId,
                Name = command.Name
            };
            Context.Labels.Add(label);
        }

        label.Color = command.Color;

        await SaveChangesAsync(token);
        return label;
    }
}
