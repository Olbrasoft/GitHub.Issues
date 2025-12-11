using Olbrasoft.GitHub.Issues.Data.Commands.IssueCommands;
using Olbrasoft.GitHub.Issues.Data.Entities;

namespace Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.CommandHandlers.IssueCommandHandlers;

/// <summary>
/// Handles command to update cached Czech summary for an issue.
/// </summary>
public class IssueUpdateSummaryCommandHandler
    : GitHubDbCommandHandler<Issue, IssueUpdateSummaryCommand, bool>
{
    public IssueUpdateSummaryCommandHandler(GitHubDbContext context) : base(context)
    {
    }

    protected override async Task<bool> ExecuteCommandAsync(
        IssueUpdateSummaryCommand command, CancellationToken token)
    {
        var issue = await Entities.FindAsync(new object[] { command.IssueId }, token);

        if (issue == null)
        {
            return false;
        }

        issue.CzechSummary = command.CzechSummary;
        issue.SummaryProvider = command.SummaryProvider;
        issue.SummaryCachedAt = DateTimeOffset.UtcNow;

        await SaveChangesAsync(token);
        return true;
    }
}
