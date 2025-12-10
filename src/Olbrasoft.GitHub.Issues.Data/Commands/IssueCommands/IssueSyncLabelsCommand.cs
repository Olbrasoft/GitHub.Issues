using Olbrasoft.Data.Cqrs;
using Olbrasoft.Mediation;

namespace Olbrasoft.GitHub.Issues.Data.Commands.IssueCommands;

/// <summary>
/// Command to sync labels for an issue.
/// Adds labels that are in the list but not on the issue.
/// Removes labels that are on the issue but not in the list.
/// </summary>
public class IssueSyncLabelsCommand : BaseCommand<bool>
{
    public int IssueId { get; set; }
    public int RepositoryId { get; set; }
    public List<string> LabelNames { get; set; } = new();

    public IssueSyncLabelsCommand(ICommandExecutor executor) : base(executor)
    {
    }

    public IssueSyncLabelsCommand(IMediator mediator) : base(mediator)
    {
    }
}
