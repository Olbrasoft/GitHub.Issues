using Olbrasoft.Data.Cqrs;
using Olbrasoft.GitHub.Issues.Data.Commands.RepositoryCommands;
using Olbrasoft.GitHub.Issues.Data.Entities;
using Olbrasoft.GitHub.Issues.Data.Queries.RepositoryQueries;
using Olbrasoft.Mediation;

namespace Olbrasoft.GitHub.Issues.Business.Services;

/// <summary>
/// Business service for repository sync operations.
/// </summary>
public class RepositorySyncBusinessService : Service, IRepositorySyncBusinessService
{
    public RepositorySyncBusinessService(IMediator mediator) : base(mediator)
    {
    }

    public async Task<Repository?> GetByFullNameAsync(string fullName, CancellationToken ct = default)
    {
        var query = new RepositoryByFullNameQuery(Mediator)
        {
            FullName = fullName
        };
        return await query.ToResultAsync(ct);
    }

    public async Task<Repository> SaveRepositoryAsync(long gitHubId, string fullName, string htmlUrl, CancellationToken ct = default)
    {
        var command = new RepositorySaveCommand(Mediator)
        {
            GitHubId = gitHubId,
            FullName = fullName,
            HtmlUrl = htmlUrl
        };
        return await command.ToResultAsync(ct);
    }

    public async Task<bool> UpdateLastSyncedAsync(int repositoryId, DateTimeOffset lastSyncedAt, CancellationToken ct = default)
    {
        var command = new RepositoryUpdateLastSyncedCommand(Mediator)
        {
            RepositoryId = repositoryId,
            LastSyncedAt = lastSyncedAt
        };
        return await command.ToResultAsync(ct);
    }

    public async Task<bool> ResetLastSyncedAsync(string fullName, CancellationToken ct = default)
    {
        var command = new RepositoryResetLastSyncedCommand(Mediator)
        {
            FullName = fullName
        };
        return await command.ToResultAsync(ct);
    }
}
