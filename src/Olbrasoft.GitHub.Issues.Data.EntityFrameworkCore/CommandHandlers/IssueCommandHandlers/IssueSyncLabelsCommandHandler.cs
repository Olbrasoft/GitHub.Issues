using Microsoft.EntityFrameworkCore;
using Olbrasoft.GitHub.Issues.Data.Commands.IssueCommands;
using Olbrasoft.GitHub.Issues.Data.Entities;

namespace Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.CommandHandlers.IssueCommandHandlers;

/// <summary>
/// Handles command to sync labels for an issue.
/// Adds labels that are in the list but not on the issue.
/// Removes labels that are on the issue but not in the list.
/// </summary>
public class IssueSyncLabelsCommandHandler
    : GitHubDbCommandHandler<Issue, IssueSyncLabelsCommand, bool>
{
    public IssueSyncLabelsCommandHandler(GitHubDbContext context) : base(context)
    {
        ArgumentNullException.ThrowIfNull(context);
    }

    protected override async Task<bool> ExecuteCommandAsync(
        IssueSyncLabelsCommand command, CancellationToken token)
    {
        var issue = await Entities
            .Include(i => i.IssueLabels)
            .ThenInclude(il => il.Label)
            .FirstOrDefaultAsync(i => i.Id == command.IssueId, token);

        if (issue == null)
        {
            return false;
        }

        // Get labels for this repository that match the names
        var labelsDict = await Context.Labels
            .Where(l => l.RepositoryId == command.RepositoryId
                     && command.LabelNames.Contains(l.Name))
            .ToDictionaryAsync(l => l.Name, token);

        // Current label names on the issue
        var currentLabelNames = issue.IssueLabels
            .Select(il => il.Label.Name)
            .ToHashSet();

        // Remove labels no longer in the list
        var labelsToRemove = issue.IssueLabels
            .Where(il => !command.LabelNames.Contains(il.Label.Name))
            .ToList();

        foreach (var labelToRemove in labelsToRemove)
        {
            issue.IssueLabels.Remove(labelToRemove);
        }

        // Add labels that are new
        foreach (var labelName in command.LabelNames)
        {
            if (!currentLabelNames.Contains(labelName) && labelsDict.TryGetValue(labelName, out var label))
            {
                issue.IssueLabels.Add(new IssueLabel
                {
                    IssueId = issue.Id,
                    LabelId = label.Id
                });
            }
        }

        await SaveChangesAsync(token);
        return true;
    }
}
