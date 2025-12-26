using Microsoft.EntityFrameworkCore;
using Olbrasoft.GitHub.Issues.Data.Commands.RepositoryCommands;
using Olbrasoft.GitHub.Issues.Data.Entities;

namespace Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.CommandHandlers.RepositoryCommandHandlers;

/// <summary>
/// Handles command to save (create or update) a repository.
/// </summary>
public class RepositorySaveCommandHandler
    : GitHubDbCommandHandler<Repository, RepositorySaveCommand, Repository>
{
    public RepositorySaveCommandHandler(GitHubDbContext context) : base(context)
    {
        ArgumentNullException.ThrowIfNull(context);
    }

    protected override async Task<Repository> ExecuteCommandAsync(
        RepositorySaveCommand command, CancellationToken token)
    {
        var repository = await Entities
            .FirstOrDefaultAsync(r => r.FullName == command.FullName, token);

        if (repository == null)
        {
            repository = new Repository
            {
                FullName = command.FullName
            };
            Context.Repositories.Add(repository);
        }

        repository.GitHubId = command.GitHubId;
        repository.HtmlUrl = command.HtmlUrl;

        await SaveChangesAsync(token);
        return repository;
    }
}
