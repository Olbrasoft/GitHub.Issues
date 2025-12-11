using Microsoft.EntityFrameworkCore;
using Olbrasoft.GitHub.Issues.Data.Commands.IssueCommands;
using Olbrasoft.GitHub.Issues.Data.Entities;

namespace Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.CommandHandlers.IssueCommandHandlers;

/// <summary>
/// Handles command to update the comment count of an issue.
/// </summary>
public class IssueUpdateCommentCountCommandHandler
    : GitHubDbCommandHandler<Issue, IssueUpdateCommentCountCommand, bool>
{
    public IssueUpdateCommentCountCommandHandler(GitHubDbContext context) : base(context)
    {
    }

    protected override async Task<bool> ExecuteCommandAsync(
        IssueUpdateCommentCountCommand command, CancellationToken token)
    {
        var updated = await Entities
            .Where(i => i.RepositoryId == command.RepositoryId && i.Number == command.IssueNumber)
            .ExecuteUpdateAsync(s => s
                .SetProperty(i => i.CommentCount, command.CommentCount)
                .SetProperty(i => i.SyncedAt, DateTimeOffset.UtcNow),
                token);

        return updated > 0;
    }
}
