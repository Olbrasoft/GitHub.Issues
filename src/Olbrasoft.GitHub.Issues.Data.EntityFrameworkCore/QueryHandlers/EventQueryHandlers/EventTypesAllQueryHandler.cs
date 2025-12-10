using Microsoft.EntityFrameworkCore;
using Olbrasoft.GitHub.Issues.Data.Entities;
using Olbrasoft.GitHub.Issues.Data.Queries.EventQueries;

namespace Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.QueryHandlers.EventQueryHandlers;

/// <summary>
/// Handles query to get all event types as a dictionary.
/// </summary>
public class EventTypesAllQueryHandler
    : GitHubDbQueryHandler<EventType, EventTypesAllQuery, Dictionary<string, EventType>>
{
    public EventTypesAllQueryHandler(GitHubDbContext context) : base(context)
    {
    }

    protected override async Task<Dictionary<string, EventType>> GetResultToHandleAsync(
        EventTypesAllQuery query, CancellationToken token)
    {
        return await Entities.ToDictionaryAsync(et => et.Name, token);
    }
}
