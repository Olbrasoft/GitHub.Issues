using Microsoft.EntityFrameworkCore;
using Olbrasoft.GitHub.Issues.Data.Commands.IssueCommands;
using Olbrasoft.GitHub.Issues.Data.Entities;

namespace Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.CommandHandlers.IssueCommandHandlers;

/// <summary>
/// Handles command to save (create or update) an issue.
/// </summary>
public class IssueSaveCommandHandler
    : GitHubDbCommandHandler<Issue, IssueSaveCommand, Issue>
{
    public IssueSaveCommandHandler(GitHubDbContext context) : base(context)
    {
    }

    protected override async Task<Issue> ExecuteCommandAsync(
        IssueSaveCommand command, CancellationToken token)
    {
        var issue = await Entities
            .FirstOrDefaultAsync(i => i.RepositoryId == command.RepositoryId
                                   && i.Number == command.Number, token);

        if (issue == null)
        {
            issue = new Issue
            {
                RepositoryId = command.RepositoryId,
                Number = command.Number
            };
            Context.Issues.Add(issue);
        }

        issue.Title = command.Title;
        issue.IsOpen = command.IsOpen;
        issue.Url = command.Url;
        issue.GitHubUpdatedAt = command.GitHubUpdatedAt;
        issue.SyncedAt = command.SyncedAt;

        if (command.Embedding != null)
        {
            issue.Embedding = command.Embedding;
        }

        if (command.CommentCount.HasValue)
        {
            issue.CommentCount = command.CommentCount.Value;
        }

        await SaveChangesAsync(token);
        return issue;
    }
}
