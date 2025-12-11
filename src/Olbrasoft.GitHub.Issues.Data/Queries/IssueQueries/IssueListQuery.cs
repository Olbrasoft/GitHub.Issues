using Olbrasoft.Data.Cqrs;
using Olbrasoft.GitHub.Issues.Data.Dtos;
using Olbrasoft.Mediation;

namespace Olbrasoft.GitHub.Issues.Data.Queries.IssueQueries;

/// <summary>
/// Query for listing issues without semantic search (for browsing repos).
/// </summary>
public class IssueListQuery : BaseQuery<IssueSearchPageDto>
{
    /// <summary>Filter by state: "open", "closed", or "all".</summary>
    public string State { get; set; } = "all";

    /// <summary>Current page number (1-based).</summary>
    public int Page { get; set; } = 1;

    /// <summary>Number of results per page.</summary>
    public int PageSize { get; set; } = 10;

    /// <summary>Required list of repository IDs to filter results.</summary>
    public IReadOnlyList<int> RepositoryIds { get; set; } = Array.Empty<int>();

    public IssueListQuery(IQueryProcessor processor) : base(processor)
    {
    }

    public IssueListQuery(IMediator mediator) : base(mediator)
    {
    }
}
