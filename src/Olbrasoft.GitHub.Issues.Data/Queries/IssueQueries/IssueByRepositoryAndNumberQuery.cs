using Olbrasoft.Data.Cqrs;
using Olbrasoft.GitHub.Issues.Data.Entities;
using Olbrasoft.Mediation;

namespace Olbrasoft.GitHub.Issues.Data.Queries.IssueQueries;

/// <summary>
/// Query to get a single issue by repository ID and issue number.
/// </summary>
public class IssueByRepositoryAndNumberQuery : BaseQuery<Issue?>
{
    public int RepositoryId { get; set; }
    public int Number { get; set; }

    public IssueByRepositoryAndNumberQuery(IQueryProcessor processor) : base(processor)
    {
    }

    public IssueByRepositoryAndNumberQuery(IMediator mediator) : base(mediator)
    {
    }
}
