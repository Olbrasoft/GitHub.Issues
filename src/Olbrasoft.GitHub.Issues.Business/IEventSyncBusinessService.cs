using Olbrasoft.GitHub.Issues.Data.Commands.EventCommands;
using Olbrasoft.GitHub.Issues.Data.Entities;

namespace Olbrasoft.GitHub.Issues.Business;

/// <summary>
/// Business service interface for event sync operations.
/// </summary>
public interface IEventSyncBusinessService
{
    /// <summary>
    /// Gets all event types as a dictionary keyed by name.
    /// </summary>
    Task<Dictionary<string, EventType>> GetAllEventTypesAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets all existing GitHub event IDs for a repository.
    /// </summary>
    Task<HashSet<long>> GetExistingEventIdsAsync(int repositoryId, CancellationToken ct = default);

    /// <summary>
    /// Saves issue events in batch. Returns number of new events saved.
    /// </summary>
    Task<int> SaveEventsBatchAsync(List<IssueEventData> events, HashSet<long> existingIds, CancellationToken ct = default);
}
