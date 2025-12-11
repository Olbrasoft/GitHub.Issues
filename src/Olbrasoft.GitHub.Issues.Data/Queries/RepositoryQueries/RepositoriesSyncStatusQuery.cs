using Olbrasoft.Data.Cqrs;
using Olbrasoft.GitHub.Issues.Data.Dtos;
using Olbrasoft.Mediation;

namespace Olbrasoft.GitHub.Issues.Data.Queries.RepositoryQueries;

/// <summary>
/// Query to get all repositories with their sync status and issue counts.
/// Used for the intelligent sync UI.
/// </summary>
public class RepositoriesSyncStatusQuery : BaseQuery<IEnumerable<RepositorySyncStatusDto>>
{
    public RepositoriesSyncStatusQuery(IQueryProcessor processor) : base(processor)
    {
    }

    public RepositoriesSyncStatusQuery(IMediator mediator) : base(mediator)
    {
    }
}
