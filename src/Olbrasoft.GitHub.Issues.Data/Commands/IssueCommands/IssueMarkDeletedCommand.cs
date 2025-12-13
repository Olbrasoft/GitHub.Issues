using Olbrasoft.Data.Cqrs;
using Olbrasoft.Mediation;

namespace Olbrasoft.GitHub.Issues.Data.Commands.IssueCommands;

/// <summary>
/// Command to mark issues as deleted (soft delete).
/// Used during full sync when issues exist in DB but not on GitHub.
/// Returns number of issues marked as deleted.
/// </summary>
public class IssueMarkDeletedCommand : BaseCommand<int>
{
    /// <summary>
    /// Repository ID (for logging/verification purposes).
    /// </summary>
    public int RepositoryId { get; set; }

    /// <summary>
    /// List of issue IDs to mark as deleted.
    /// </summary>
    public List<int> IssueIds { get; set; } = [];

    public IssueMarkDeletedCommand(ICommandExecutor executor) : base(executor)
    {
    }

    public IssueMarkDeletedCommand(IMediator mediator) : base(mediator)
    {
    }
}
