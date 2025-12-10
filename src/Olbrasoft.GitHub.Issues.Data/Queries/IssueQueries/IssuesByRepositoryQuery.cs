using Olbrasoft.Data.Cqrs;
using Olbrasoft.GitHub.Issues.Data.Entities;
using Olbrasoft.Mediation;

namespace Olbrasoft.GitHub.Issues.Data.Queries.IssueQueries;

/// <summary>
/// Query to get all issues for a repository as a dictionary keyed by issue number.
/// Used for bulk lookup during sync operations.
/// </summary>
public class IssuesByRepositoryQuery : BaseQuery<Dictionary<int, Issue>>
{
    public int RepositoryId { get; set; }

    public IssuesByRepositoryQuery(IQueryProcessor processor) : base(processor)
    {
    }

    public IssuesByRepositoryQuery(IMediator mediator) : base(mediator)
    {
    }
}
