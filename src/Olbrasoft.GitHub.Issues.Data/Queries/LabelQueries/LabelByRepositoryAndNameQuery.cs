using Olbrasoft.Data.Cqrs;
using Olbrasoft.GitHub.Issues.Data.Entities;
using Olbrasoft.Mediation;

namespace Olbrasoft.GitHub.Issues.Data.Queries.LabelQueries;

/// <summary>
/// Query to get a single label by repository ID and name.
/// </summary>
public class LabelByRepositoryAndNameQuery : BaseQuery<Label?>
{
    public int RepositoryId { get; set; }
    public string Name { get; set; } = string.Empty;

    public LabelByRepositoryAndNameQuery(IQueryProcessor processor) : base(processor)
    {
    }

    public LabelByRepositoryAndNameQuery(IMediator mediator) : base(mediator)
    {
    }
}
