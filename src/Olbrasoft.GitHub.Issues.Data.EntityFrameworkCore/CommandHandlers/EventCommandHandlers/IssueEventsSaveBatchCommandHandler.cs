using Olbrasoft.Data.Cqrs;
using Olbrasoft.GitHub.Issues.Data.Commands.EventCommands;
using Olbrasoft.GitHub.Issues.Data.Entities;
using Olbrasoft.GitHub.Issues.Data.Repositories;

namespace Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.CommandHandlers.EventCommandHandlers;

/// <summary>
/// Handles command to save issue events in batch.
/// Performs periodic saves to manage memory for large batches.
/// Uses IEventRepository abstraction to remove DIP violation.
/// </summary>
public class IssueEventsSaveBatchCommandHandler : ICommandHandler<IssueEventsSaveBatchCommand, int>
{
    private readonly IEventRepository _eventRepository;

    public IssueEventsSaveBatchCommandHandler(IEventRepository eventRepository)
    {
        ArgumentNullException.ThrowIfNull(eventRepository);

        _eventRepository = eventRepository;
    }

    public async Task<int> HandleAsync(IssueEventsSaveBatchCommand command, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Convert IssueEventData to IssueEvent entities
        var issueEvents = command.Events
            .Select(evt => new IssueEvent
            {
                IssueId = evt.IssueId,
                EventTypeId = evt.EventTypeId,
                GitHubEventId = evt.GitHubEventId,
                CreatedAt = evt.CreatedAt
            })
            .ToList();

        // Use repository to save batch (handles periodic saves internally)
        return await _eventRepository.AddEventsBatchAsync(issueEvents, command.ExistingEventIds, token);
    }
}
