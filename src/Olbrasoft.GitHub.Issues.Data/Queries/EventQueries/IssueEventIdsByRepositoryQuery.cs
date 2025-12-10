using Olbrasoft.Data.Cqrs;
using Olbrasoft.Mediation;

namespace Olbrasoft.GitHub.Issues.Data.Queries.EventQueries;

/// <summary>
/// Query to get all existing GitHub event IDs for a repository.
/// Used for deduplication during event sync.
/// </summary>
public class IssueEventIdsByRepositoryQuery : BaseQuery<HashSet<long>>
{
    public int RepositoryId { get; set; }

    public IssueEventIdsByRepositoryQuery(IQueryProcessor processor) : base(processor)
    {
    }

    public IssueEventIdsByRepositoryQuery(IMediator mediator) : base(mediator)
    {
    }
}
