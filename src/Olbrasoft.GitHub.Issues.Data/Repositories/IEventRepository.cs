using Olbrasoft.GitHub.Issues.Data.Entities;

namespace Olbrasoft.GitHub.Issues.Data.Repositories;

/// <summary>
/// Repository abstraction for EventType and IssueEvent entity operations.
/// Follows Dependency Inversion Principle - handlers depend on this abstraction,
/// not on concrete EF Core implementation.
/// </summary>
public interface IEventRepository
{
    /// <summary>
    /// Gets an event type by its ID.
    /// </summary>
    /// <param name="id">Event type ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Event type or null if not found</returns>
    Task<EventType?> GetEventTypeByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all event types as a dictionary keyed by name.
    /// Used for event sync operations to map event names to types.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Dictionary of event types keyed by name</returns>
    Task<Dictionary<string, EventType>> GetAllEventTypesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new event type to the database.
    /// </summary>
    /// <param name="eventType">Event type to add</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task AddEventTypeAsync(EventType eventType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all existing GitHub event IDs for a repository.
    /// Used to avoid duplicate event insertion during sync.
    /// </summary>
    /// <param name="repositoryId">Repository ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Set of existing GitHub event IDs</returns>
    Task<HashSet<long>> GetExistingEventIdsByRepositoryAsync(int repositoryId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds issue events in batch with periodic saves for memory management.
    /// Skips events that already exist in the provided set.
    /// </summary>
    /// <param name="events">List of issue events to add</param>
    /// <param name="existingEventIds">Set of existing event IDs to update as events are added</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of new events added</returns>
    Task<int> AddEventsBatchAsync(List<IssueEvent> events, HashSet<long> existingEventIds, CancellationToken cancellationToken = default);
}
