using Olbrasoft.Data.Cqrs;
using Olbrasoft.GitHub.Issues.Data.Entities;
using Olbrasoft.Mediation;

namespace Olbrasoft.GitHub.Issues.Data.Queries.LabelQueries;

/// <summary>
/// Query to get all labels for a repository.
/// </summary>
public class LabelsByRepositoryQuery : BaseQuery<List<Label>>
{
    public int RepositoryId { get; set; }

    public LabelsByRepositoryQuery(IQueryProcessor processor) : base(processor)
    {
    }

    public LabelsByRepositoryQuery(IMediator mediator) : base(mediator)
    {
    }
}
