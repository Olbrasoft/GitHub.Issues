using Olbrasoft.Data.Cqrs;
using Olbrasoft.GitHub.Issues.Data.Dtos;
using Olbrasoft.Mediation;

namespace Olbrasoft.GitHub.Issues.Data.Queries.RepositoryQueries;

/// <summary>
/// Query to get repositories by their IDs.
/// </summary>
public class RepositoriesByIdsQuery : BaseQuery<IEnumerable<RepositorySearchResultDto>>
{
    /// <summary>Repository IDs to fetch.</summary>
    public IReadOnlyList<int> Ids { get; set; } = Array.Empty<int>();

    public RepositoriesByIdsQuery(IQueryProcessor processor) : base(processor)
    {
    }

    public RepositoriesByIdsQuery(IMediator mediator) : base(mediator)
    {
    }
}
