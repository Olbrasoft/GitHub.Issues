using Olbrasoft.Data.Cqrs;
using Olbrasoft.GitHub.Issues.Data.Dtos;
using Olbrasoft.Mediation;
using Pgvector;

namespace Olbrasoft.GitHub.Issues.Data.Queries.IssueQueries;

/// <summary>
/// Query for searching issues by vector similarity.
/// </summary>
public class IssueSearchQuery : BaseQuery<IssueSearchPageDto>
{
    /// <summary>Pre-computed embedding vector for the search query.</summary>
    public Vector QueryEmbedding { get; set; } = null!;

    /// <summary>Filter by state: "open", "closed", or "all".</summary>
    public string State { get; set; } = "all";

    /// <summary>Current page number (1-based).</summary>
    public int Page { get; set; } = 1;

    /// <summary>Number of results per page.</summary>
    public int PageSize { get; set; } = 10;

    public IssueSearchQuery(IQueryProcessor processor) : base(processor)
    {
    }

    public IssueSearchQuery(IMediator mediator) : base(mediator)
    {
    }
}
