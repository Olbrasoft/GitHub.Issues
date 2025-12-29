using Olbrasoft.Data.Cqrs;
using Olbrasoft.GitHub.Issues.Data.Entities;
using Olbrasoft.GitHub.Issues.Data.Queries.EventQueries;
using Olbrasoft.GitHub.Issues.Data.Repositories;

namespace Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.QueryHandlers.EventQueryHandlers;

/// <summary>
/// Handles query to get all event types as a dictionary.
/// Uses IEventRepository abstraction to remove DIP violation.
/// </summary>
public class EventTypesAllQueryHandler : IQueryHandler<EventTypesAllQuery, Dictionary<string, EventType>>
{
    private readonly IEventRepository _eventRepository;

    public EventTypesAllQueryHandler(IEventRepository eventRepository)
    {
        ArgumentNullException.ThrowIfNull(eventRepository);

        _eventRepository = eventRepository;
    }

    public async Task<Dictionary<string, EventType>> HandleAsync(EventTypesAllQuery query, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(query);
        return await _eventRepository.GetAllEventTypesAsync(token);
    }
}
