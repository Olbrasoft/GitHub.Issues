using Olbrasoft.Data.Cqrs;
using Olbrasoft.Mediation;

namespace Olbrasoft.GitHub.Issues.Data.Commands.EventCommands;

/// <summary>
/// Command to save issue events in batch.
/// Returns number of new events saved.
/// </summary>
public class IssueEventsSaveBatchCommand : BaseCommand<int>
{
    public List<IssueEventData> Events { get; set; } = new();
    public HashSet<long> ExistingEventIds { get; set; } = new();

    public IssueEventsSaveBatchCommand(ICommandExecutor executor) : base(executor)
    {
    }

    public IssueEventsSaveBatchCommand(IMediator mediator) : base(mediator)
    {
    }
}

/// <summary>
/// Data transfer object for issue event creation.
/// </summary>
public record IssueEventData(
    int IssueId,
    int EventTypeId,
    long GitHubEventId,
    DateTimeOffset CreatedAt);
