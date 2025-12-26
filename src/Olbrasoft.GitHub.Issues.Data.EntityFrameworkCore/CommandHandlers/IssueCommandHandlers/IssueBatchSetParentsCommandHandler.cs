using Microsoft.EntityFrameworkCore;
using Olbrasoft.GitHub.Issues.Data.Commands.IssueCommands;
using Olbrasoft.GitHub.Issues.Data.Entities;

namespace Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.CommandHandlers.IssueCommandHandlers;

/// <summary>
/// Handles command to set parent-child relationships for multiple issues.
/// </summary>
public class IssueBatchSetParentsCommandHandler
    : GitHubDbCommandHandler<Issue, IssueBatchSetParentsCommand, int>
{
    public IssueBatchSetParentsCommandHandler(GitHubDbContext context) : base(context)
    {
        ArgumentNullException.ThrowIfNull(context);
    }

    protected override async Task<int> ExecuteCommandAsync(
        IssueBatchSetParentsCommand command, CancellationToken token)
    {
        if (command.ChildToParentMap.Count == 0)
        {
            return 0;
        }

        var childIds = command.ChildToParentMap.Keys.ToList();
        var issues = await Entities
            .Where(i => childIds.Contains(i.Id))
            .ToListAsync(token);

        var updatedCount = 0;
        foreach (var issue in issues)
        {
            if (command.ChildToParentMap.TryGetValue(issue.Id, out var parentId))
            {
                if (issue.ParentIssueId != parentId)
                {
                    issue.ParentIssueId = parentId;
                    updatedCount++;
                }
            }
        }

        if (updatedCount > 0)
        {
            await SaveChangesAsync(token);
        }

        return updatedCount;
    }
}
