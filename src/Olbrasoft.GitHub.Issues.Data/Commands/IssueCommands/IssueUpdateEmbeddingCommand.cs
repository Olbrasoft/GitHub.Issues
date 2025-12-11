using Olbrasoft.Data.Cqrs;
using Olbrasoft.Mediation;

namespace Olbrasoft.GitHub.Issues.Data.Commands.IssueCommands;

/// <summary>
/// Command to update embedding for an existing issue.
/// </summary>
public class IssueUpdateEmbeddingCommand : BaseCommand<bool>
{
    public int IssueId { get; set; }
    public float[] Embedding { get; set; } = null!;

    public IssueUpdateEmbeddingCommand(ICommandExecutor executor) : base(executor)
    {
    }

    public IssueUpdateEmbeddingCommand(IMediator mediator) : base(mediator)
    {
    }
}
