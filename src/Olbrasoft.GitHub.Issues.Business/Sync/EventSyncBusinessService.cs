using Olbrasoft.Data.Cqrs;
using Olbrasoft.GitHub.Issues.Data.Commands.EventCommands;
using Olbrasoft.GitHub.Issues.Data.Entities;
using Olbrasoft.GitHub.Issues.Data.Queries.EventQueries;
using Olbrasoft.Mediation;

namespace Olbrasoft.GitHub.Issues.Business.Sync;

/// <summary>
/// Business service for event sync operations.
/// </summary>
public class EventSyncBusinessService : Service, IEventSyncBusinessService
{
    public EventSyncBusinessService(IMediator mediator) : base(mediator)
    {
        ArgumentNullException.ThrowIfNull(mediator);
    }

    public async Task<Dictionary<string, EventType>> GetAllEventTypesAsync(CancellationToken ct = default)
    {
        var query = new EventTypesAllQuery(Mediator);
        return await query.ToResultAsync(ct);
    }

    public async Task<HashSet<long>> GetExistingEventIdsAsync(int repositoryId, CancellationToken ct = default)
    {
        var query = new IssueEventIdsByRepositoryQuery(Mediator)
        {
            RepositoryId = repositoryId
        };
        return await query.ToResultAsync(ct);
    }

    public async Task<int> SaveEventsBatchAsync(List<IssueEventData> events, HashSet<long> existingIds, CancellationToken ct = default)
    {
        var command = new IssueEventsSaveBatchCommand(Mediator)
        {
            Events = events,
            ExistingEventIds = existingIds
        };
        return await command.ToResultAsync(ct);
    }
}
