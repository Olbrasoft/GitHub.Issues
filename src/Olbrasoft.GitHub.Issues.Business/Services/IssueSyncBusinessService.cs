using Olbrasoft.Data.Cqrs;
using Olbrasoft.GitHub.Issues.Data.Commands.IssueCommands;
using Olbrasoft.GitHub.Issues.Data.Entities;
using Olbrasoft.GitHub.Issues.Data.Queries.IssueQueries;
using Olbrasoft.Mediation;

namespace Olbrasoft.GitHub.Issues.Business.Services;

/// <summary>
/// Business service for issue sync operations.
/// </summary>
public class IssueSyncBusinessService : Service, IIssueSyncBusinessService
{
    public IssueSyncBusinessService(IMediator mediator) : base(mediator)
    {
        ArgumentNullException.ThrowIfNull(mediator);
    }

    public async Task<Issue?> GetIssueAsync(int repositoryId, int number, CancellationToken ct = default)
    {
        var query = new IssueByRepositoryAndNumberQuery(Mediator)
        {
            RepositoryId = repositoryId,
            Number = number
        };
        return await query.ToResultAsync(ct);
    }

    public async Task<Dictionary<int, Issue>> GetIssuesByRepositoryAsync(int repositoryId, CancellationToken ct = default)
    {
        var query = new IssuesByRepositoryQuery(Mediator)
        {
            RepositoryId = repositoryId
        };
        return await query.ToResultAsync(ct);
    }

    public async Task<Issue> SaveIssueAsync(
        int repositoryId,
        int number,
        string title,
        bool isOpen,
        string url,
        DateTimeOffset gitHubUpdatedAt,
        DateTimeOffset syncedAt,
        float[] embedding,
        CancellationToken ct = default)
    {
        var command = new IssueSaveCommand(Mediator)
        {
            RepositoryId = repositoryId,
            Number = number,
            Title = title,
            IsOpen = isOpen,
            Url = url,
            GitHubUpdatedAt = gitHubUpdatedAt,
            SyncedAt = syncedAt,
            Embedding = embedding
        };
        return await command.ToResultAsync(ct);
    }

    public async Task<bool> UpdateEmbeddingAsync(int issueId, float[] embedding, CancellationToken ct = default)
    {
        var command = new IssueUpdateEmbeddingCommand(Mediator)
        {
            IssueId = issueId,
            Embedding = embedding
        };
        return await command.ToResultAsync(ct);
    }

    public async Task<int> BatchSetParentsAsync(Dictionary<int, int?> childToParentMap, CancellationToken ct = default)
    {
        var command = new IssueBatchSetParentsCommand(Mediator)
        {
            ChildToParentMap = childToParentMap
        };
        return await command.ToResultAsync(ct);
    }

    public async Task<bool> SyncLabelsAsync(int issueId, int repositoryId, List<string> labelNames, CancellationToken ct = default)
    {
        var command = new IssueSyncLabelsCommand(Mediator)
        {
            IssueId = issueId,
            RepositoryId = repositoryId,
            LabelNames = labelNames
        };
        return await command.ToResultAsync(ct);
    }

    public async Task<int> MarkIssuesAsDeletedAsync(int repositoryId, IEnumerable<int> issueIdsToDelete, CancellationToken ct = default)
    {
        var command = new IssueMarkDeletedCommand(Mediator)
        {
            RepositoryId = repositoryId,
            IssueIds = issueIdsToDelete.ToList()
        };
        return await command.ToResultAsync(ct);
    }
}
