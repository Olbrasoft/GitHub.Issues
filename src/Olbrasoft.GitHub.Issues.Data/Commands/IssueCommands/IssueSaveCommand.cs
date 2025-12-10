using Olbrasoft.Data.Cqrs;
using Olbrasoft.GitHub.Issues.Data.Entities;
using Olbrasoft.Mediation;
using Pgvector;

namespace Olbrasoft.GitHub.Issues.Data.Commands.IssueCommands;

/// <summary>
/// Command to save (create or update) an issue.
/// </summary>
public class IssueSaveCommand : BaseCommand<Issue>
{
    public int RepositoryId { get; set; }
    public int Number { get; set; }
    public string Title { get; set; } = string.Empty;
    public bool IsOpen { get; set; }
    public string Url { get; set; } = string.Empty;
    public DateTimeOffset GitHubUpdatedAt { get; set; }
    public DateTimeOffset SyncedAt { get; set; }
    public Vector? Embedding { get; set; }

    public IssueSaveCommand(ICommandExecutor executor) : base(executor)
    {
    }

    public IssueSaveCommand(IMediator mediator) : base(mediator)
    {
    }
}
