using Microsoft.EntityFrameworkCore;
using Olbrasoft.GitHub.Issues.Data.Entities;
using Olbrasoft.GitHub.Issues.Data.Repositories;

namespace Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.Repositories;

/// <summary>
/// Entity Framework Core implementation of IEventRepository.
/// </summary>
public class EfCoreEventRepository : IEventRepository
{
    private readonly GitHubDbContext _context;
    private const int BatchSaveSize = 100;

    public EfCoreEventRepository(GitHubDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        _context = context;
    }

    public async Task<EventType?> GetEventTypeByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.EventTypes.FindAsync(new object[] { id }, cancellationToken);
    }

    public async Task<Dictionary<string, EventType>> GetAllEventTypesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.EventTypes.ToDictionaryAsync(et => et.Name, cancellationToken);
    }

    public async Task AddEventTypeAsync(EventType eventType, CancellationToken cancellationToken = default)
    {
        _context.EventTypes.Add(eventType);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<HashSet<long>> GetExistingEventIdsByRepositoryAsync(int repositoryId, CancellationToken cancellationToken = default)
    {
        var eventIds = await _context.IssueEvents
            .Where(e => e.Issue.RepositoryId == repositoryId)
            .Select(e => e.GitHubEventId)
            .ToListAsync(cancellationToken);

        return new HashSet<long>(eventIds);
    }

    public async Task<int> AddEventsBatchAsync(List<IssueEvent> events, HashSet<long> existingEventIds, CancellationToken cancellationToken = default)
    {
        var newCount = 0;
        var batch = new List<IssueEvent>();

        foreach (var evt in events)
        {
            // Skip if already exists
            if (existingEventIds.Contains(evt.GitHubEventId))
            {
                continue;
            }

            batch.Add(evt);
            existingEventIds.Add(evt.GitHubEventId);
            newCount++;

            // Periodic save for memory management
            if (batch.Count >= BatchSaveSize)
            {
                _context.IssueEvents.AddRange(batch);
                await _context.SaveChangesAsync(cancellationToken);
                batch.Clear();
            }
        }

        // Save remaining events
        if (batch.Count > 0)
        {
            _context.IssueEvents.AddRange(batch);
            await _context.SaveChangesAsync(cancellationToken);
        }

        return newCount;
    }
}
