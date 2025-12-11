using Olbrasoft.Data.Cqrs;
using Olbrasoft.Mediation;

namespace Olbrasoft.GitHub.Issues.Data.Commands.IssueCommands;

/// <summary>
/// Command to update the comment count of an issue.
/// Lightweight operation - doesn't regenerate embeddings.
/// </summary>
public class IssueUpdateCommentCountCommand : BaseCommand<bool>
{
    public int RepositoryId { get; set; }
    public int IssueNumber { get; set; }
    public int CommentCount { get; set; }

    public IssueUpdateCommentCountCommand(ICommandExecutor executor) : base(executor)
    {
    }

    public IssueUpdateCommentCountCommand(IMediator mediator) : base(mediator)
    {
    }
}
