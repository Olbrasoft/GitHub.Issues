using Olbrasoft.Data.Cqrs;
using Olbrasoft.GitHub.Issues.Data.Dtos;
using Olbrasoft.Mediation;

namespace Olbrasoft.GitHub.Issues.Data.Queries.IssueQueries;

/// <summary>
/// Query for finding issues by their issue numbers (exact match).
/// Used for hybrid search when user searches by issue number pattern.
/// </summary>
public class IssuesByNumbersQuery : BaseQuery<List<IssueSearchResultDto>>
{
    /// <summary>List of issue numbers to find.</summary>
    public IReadOnlyList<int> IssueNumbers { get; set; } = Array.Empty<int>();

    /// <summary>Optional repository filter (full name like "owner/repo" or partial).</summary>
    public string? RepositoryName { get; set; }

    /// <summary>Optional list of repository IDs to filter results.</summary>
    public IReadOnlyList<int>? RepositoryIds { get; set; }

    /// <summary>Filter by state: "open", "closed", or "all".</summary>
    public string State { get; set; } = "all";

    public IssuesByNumbersQuery(IQueryProcessor processor) : base(processor)
    {
    }

    public IssuesByNumbersQuery(IMediator mediator) : base(mediator)
    {
    }
}
