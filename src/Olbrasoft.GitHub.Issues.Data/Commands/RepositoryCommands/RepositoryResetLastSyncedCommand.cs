using Olbrasoft.Data.Cqrs;
using Olbrasoft.Mediation;

namespace Olbrasoft.GitHub.Issues.Data.Commands.RepositoryCommands;

/// <summary>
/// Command to reset LastSyncedAt to NULL for a repository (forces full sync).
/// </summary>
public class RepositoryResetLastSyncedCommand : BaseCommand<bool>
{
    public string FullName { get; set; } = string.Empty;

    public RepositoryResetLastSyncedCommand(ICommandExecutor executor) : base(executor)
    {
    }

    public RepositoryResetLastSyncedCommand(IMediator mediator) : base(mediator)
    {
    }
}
