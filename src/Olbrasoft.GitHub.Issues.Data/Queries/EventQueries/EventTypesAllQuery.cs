using Olbrasoft.Data.Cqrs;
using Olbrasoft.GitHub.Issues.Data.Entities;
using Olbrasoft.Mediation;

namespace Olbrasoft.GitHub.Issues.Data.Queries.EventQueries;

/// <summary>
/// Query to get all event types as a dictionary keyed by name.
/// Used for caching during event sync operations.
/// </summary>
public class EventTypesAllQuery : BaseQuery<Dictionary<string, EventType>>
{
    public EventTypesAllQuery(IQueryProcessor processor) : base(processor)
    {
    }

    public EventTypesAllQuery(IMediator mediator) : base(mediator)
    {
    }
}
