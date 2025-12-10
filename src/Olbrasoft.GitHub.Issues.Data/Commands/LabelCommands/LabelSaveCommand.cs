using Olbrasoft.Data.Cqrs;
using Olbrasoft.GitHub.Issues.Data.Entities;
using Olbrasoft.Mediation;

namespace Olbrasoft.GitHub.Issues.Data.Commands.LabelCommands;

/// <summary>
/// Command to save (create or update) a label.
/// </summary>
public class LabelSaveCommand : BaseCommand<Label>
{
    public int RepositoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = "ededed";

    public LabelSaveCommand(ICommandExecutor executor) : base(executor)
    {
    }

    public LabelSaveCommand(IMediator mediator) : base(mediator)
    {
    }
}
