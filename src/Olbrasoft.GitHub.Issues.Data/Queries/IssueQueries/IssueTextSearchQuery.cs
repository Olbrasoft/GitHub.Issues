using Olbrasoft.Data.Cqrs;
using Olbrasoft.GitHub.Issues.Data.Dtos;
using Olbrasoft.Mediation;

namespace Olbrasoft.GitHub.Issues.Data.Queries.IssueQueries;

/// <summary>
/// Query for text-based search (LIKE pattern matching) as fallback when semantic search is unavailable.
/// Searches in issue title and body text.
/// </summary>
public class IssueTextSearchQuery : BaseQuery<IssueSearchPageDto>
{
    /// <summary>Search text to match against title and body.</summary>
    public string SearchText { get; set; } = string.Empty;

    /// <summary>Filter by state: "open", "closed", or "all".</summary>
    public string State { get; set; } = "all";

    /// <summary>Current page number (1-based).</summary>
    public int Page { get; set; } = 1;

    /// <summary>Number of results per page.</summary>
    public int PageSize { get; set; } = 10;

    /// <summary>Optional list of repository IDs to filter results.</summary>
    public IReadOnlyList<int>? RepositoryIds { get; set; }

    public IssueTextSearchQuery(IQueryProcessor processor) : base(processor)
    {
    }

    public IssueTextSearchQuery(IMediator mediator) : base(mediator)
    {
    }
}
