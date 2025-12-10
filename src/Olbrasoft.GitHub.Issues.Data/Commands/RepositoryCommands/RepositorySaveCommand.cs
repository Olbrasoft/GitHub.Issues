using Olbrasoft.Data.Cqrs;
using Olbrasoft.GitHub.Issues.Data.Entities;
using Olbrasoft.Mediation;

namespace Olbrasoft.GitHub.Issues.Data.Commands.RepositoryCommands;

/// <summary>
/// Command to save (create or update) a repository.
/// </summary>
public class RepositorySaveCommand : BaseCommand<Repository>
{
    public long GitHubId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string HtmlUrl { get; set; } = string.Empty;

    public RepositorySaveCommand(ICommandExecutor executor) : base(executor)
    {
    }

    public RepositorySaveCommand(IMediator mediator) : base(mediator)
    {
    }
}
