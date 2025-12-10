using Olbrasoft.Data.Cqrs;
using Olbrasoft.Mediation;

namespace Olbrasoft.GitHub.Issues.Data.Commands.IssueCommands;

/// <summary>
/// Command to set parent-child relationships for multiple issues in batch.
/// Returns number of relationships updated.
/// </summary>
public class IssueBatchSetParentsCommand : BaseCommand<int>
{
    /// <summary>
    /// Dictionary mapping child issue ID to parent issue ID (or null to remove parent).
    /// </summary>
    public Dictionary<int, int?> ChildToParentMap { get; set; } = new();

    public IssueBatchSetParentsCommand(ICommandExecutor executor) : base(executor)
    {
    }

    public IssueBatchSetParentsCommand(IMediator mediator) : base(mediator)
    {
    }
}
