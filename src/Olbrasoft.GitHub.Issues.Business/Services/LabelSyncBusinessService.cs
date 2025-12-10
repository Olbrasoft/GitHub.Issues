using Olbrasoft.Data.Cqrs;
using Olbrasoft.GitHub.Issues.Data.Commands.LabelCommands;
using Olbrasoft.GitHub.Issues.Data.Entities;
using Olbrasoft.GitHub.Issues.Data.Queries.LabelQueries;
using Olbrasoft.Mediation;

namespace Olbrasoft.GitHub.Issues.Business.Services;

/// <summary>
/// Business service for label sync operations.
/// </summary>
public class LabelSyncBusinessService : Service, ILabelSyncBusinessService
{
    public LabelSyncBusinessService(IMediator mediator) : base(mediator)
    {
    }

    public async Task<Label?> GetLabelAsync(int repositoryId, string name, CancellationToken ct = default)
    {
        var query = new LabelByRepositoryAndNameQuery(Mediator)
        {
            RepositoryId = repositoryId,
            Name = name
        };
        return await query.ToResultAsync(ct);
    }

    public async Task<List<Label>> GetLabelsByRepositoryAsync(int repositoryId, CancellationToken ct = default)
    {
        var query = new LabelsByRepositoryQuery(Mediator)
        {
            RepositoryId = repositoryId
        };
        return await query.ToResultAsync(ct);
    }

    public async Task<Label> SaveLabelAsync(int repositoryId, string name, string color, CancellationToken ct = default)
    {
        var command = new LabelSaveCommand(Mediator)
        {
            RepositoryId = repositoryId,
            Name = name,
            Color = color
        };
        return await command.ToResultAsync(ct);
    }
}
