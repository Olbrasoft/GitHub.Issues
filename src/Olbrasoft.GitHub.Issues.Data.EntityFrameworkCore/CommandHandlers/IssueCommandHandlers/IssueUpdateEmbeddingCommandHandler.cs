using Microsoft.EntityFrameworkCore;
using Olbrasoft.GitHub.Issues.Data.Commands.IssueCommands;
using Olbrasoft.GitHub.Issues.Data.Entities;

namespace Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.CommandHandlers.IssueCommandHandlers;

/// <summary>
/// Handles command to update embedding for an existing issue.
/// </summary>
public class IssueUpdateEmbeddingCommandHandler
    : GitHubDbCommandHandler<Issue, IssueUpdateEmbeddingCommand, bool>
{
    public IssueUpdateEmbeddingCommandHandler(GitHubDbContext context) : base(context)
    {
    }

    protected override async Task<bool> ExecuteCommandAsync(
        IssueUpdateEmbeddingCommand command, CancellationToken token)
    {
        var issue = await Entities.FindAsync(new object[] { command.IssueId }, token);

        if (issue == null)
        {
            return false;
        }

        issue.Embedding = command.Embedding;
        await SaveChangesAsync(token);
        return true;
    }
}
