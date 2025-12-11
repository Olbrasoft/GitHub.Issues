using Olbrasoft.Data.Cqrs;
using Olbrasoft.Mediation;

namespace Olbrasoft.GitHub.Issues.Data.Commands.IssueCommands;

/// <summary>
/// Command to update the cached Czech summary for an issue.
/// </summary>
public class IssueUpdateSummaryCommand : BaseCommand<bool>
{
    public int IssueId { get; set; }
    public string? CzechSummary { get; set; }
    public string? SummaryProvider { get; set; }

    public IssueUpdateSummaryCommand(ICommandExecutor executor) : base(executor)
    {
    }

    public IssueUpdateSummaryCommand(IMediator mediator) : base(mediator)
    {
    }
}
