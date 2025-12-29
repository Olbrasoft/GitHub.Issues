using Olbrasoft.Data.Cqrs;
using Olbrasoft.GitHub.Issues.Data.Queries.EventQueries;
using Olbrasoft.GitHub.Issues.Data.Repositories;

namespace Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.QueryHandlers.EventQueryHandlers;

/// <summary>
/// Handles query to get all existing GitHub event IDs for a repository.
/// Uses IEventRepository abstraction to remove DIP violation.
/// </summary>
public class IssueEventIdsByRepositoryQueryHandler : IQueryHandler<IssueEventIdsByRepositoryQuery, HashSet<long>>
{
    private readonly IEventRepository _eventRepository;

    public IssueEventIdsByRepositoryQueryHandler(IEventRepository eventRepository)
    {
        ArgumentNullException.ThrowIfNull(eventRepository);

        _eventRepository = eventRepository;
    }

    public async Task<HashSet<long>> HandleAsync(IssueEventIdsByRepositoryQuery query, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(query);
        return await _eventRepository.GetExistingEventIdsByRepositoryAsync(query.RepositoryId, token);
    }
}
