using Olbrasoft.Data.Cqrs;
using Olbrasoft.GitHub.Issues.Data.Dtos;
using Olbrasoft.Mediation;

namespace Olbrasoft.GitHub.Issues.Data.Queries.RepositoryQueries;

/// <summary>
/// Query to search repositories by name pattern.
/// </summary>
public class RepositoriesSearchQuery : BaseQuery<IEnumerable<RepositorySearchResultDto>>
{
    /// <summary>Search term to match against repository full names.</summary>
    public string Term { get; set; } = string.Empty;

    /// <summary>Maximum number of results to return.</summary>
    public int MaxResults { get; set; } = 15;

    public RepositoriesSearchQuery(IQueryProcessor processor) : base(processor)
    {
    }

    public RepositoriesSearchQuery(IMediator mediator) : base(mediator)
    {
    }
}
