using Olbrasoft.Data.Cqrs;
using Olbrasoft.Mediation;

namespace Olbrasoft.GitHub.Issues.Data.Commands.RepositoryCommands;

/// <summary>
/// Command to update LastSyncedAt timestamp for a repository.
/// </summary>
public class RepositoryUpdateLastSyncedCommand : BaseCommand<bool>
{
    public int RepositoryId { get; set; }
    public DateTimeOffset LastSyncedAt { get; set; }

    public RepositoryUpdateLastSyncedCommand(ICommandExecutor executor) : base(executor)
    {
    }

    public RepositoryUpdateLastSyncedCommand(IMediator mediator) : base(mediator)
    {
    }
}
