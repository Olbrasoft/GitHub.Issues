using Olbrasoft.GitHub.Issues.Data.Commands.EventCommands;
using Olbrasoft.GitHub.Issues.Data.Entities;

namespace Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.CommandHandlers.EventCommandHandlers;

/// <summary>
/// Handles command to save issue events in batch.
/// Performs periodic saves to manage memory for large batches.
/// </summary>
public class IssueEventsSaveBatchCommandHandler
    : GitHubDbCommandHandler<IssueEvent, IssueEventsSaveBatchCommand, int>
{
    private const int BatchSaveSize = 100;

    public IssueEventsSaveBatchCommandHandler(GitHubDbContext context) : base(context)
    {
        ArgumentNullException.ThrowIfNull(context);
    }

    protected override async Task<int> ExecuteCommandAsync(
        IssueEventsSaveBatchCommand command, CancellationToken token)
    {
        var newCount = 0;
        var batch = new List<IssueEvent>();

        foreach (var evt in command.Events)
        {
            // Skip if already exists
            if (command.ExistingEventIds.Contains(evt.GitHubEventId))
            {
                continue;
            }

            var issueEvent = new IssueEvent
            {
                IssueId = evt.IssueId,
                EventTypeId = evt.EventTypeId,
                GitHubEventId = evt.GitHubEventId,
                CreatedAt = evt.CreatedAt
            };

            batch.Add(issueEvent);
            command.ExistingEventIds.Add(evt.GitHubEventId);
            newCount++;

            // Periodic save
            if (batch.Count >= BatchSaveSize)
            {
                Context.IssueEvents.AddRange(batch);
                await SaveChangesAsync(token);
                batch.Clear();
            }
        }

        // Save remaining
        if (batch.Count > 0)
        {
            Context.IssueEvents.AddRange(batch);
            await SaveChangesAsync(token);
        }

        return newCount;
    }
}
