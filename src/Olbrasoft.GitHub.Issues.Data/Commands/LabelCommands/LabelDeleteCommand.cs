using Olbrasoft.Data.Cqrs;
using Olbrasoft.Mediation;

namespace Olbrasoft.GitHub.Issues.Data.Commands.LabelCommands;

/// <summary>
/// Command to delete a label from a repository.
/// </summary>
public class LabelDeleteCommand : BaseCommand<bool>
{
    public int RepositoryId { get; set; }
    public string Name { get; set; } = string.Empty;

    public LabelDeleteCommand(ICommandExecutor executor) : base(executor)
    {
    }

    public LabelDeleteCommand(IMediator mediator) : base(mediator)
    {
    }
}
