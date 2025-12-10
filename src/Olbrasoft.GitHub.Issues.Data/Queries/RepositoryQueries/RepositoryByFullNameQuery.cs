using Olbrasoft.Data.Cqrs;
using Olbrasoft.GitHub.Issues.Data.Entities;
using Olbrasoft.Mediation;

namespace Olbrasoft.GitHub.Issues.Data.Queries.RepositoryQueries;

/// <summary>
/// Query to get a repository by its full name (owner/repo).
/// </summary>
public class RepositoryByFullNameQuery : BaseQuery<Repository?>
{
    public string FullName { get; set; } = string.Empty;

    public RepositoryByFullNameQuery(IQueryProcessor processor) : base(processor)
    {
    }

    public RepositoryByFullNameQuery(IMediator mediator) : base(mediator)
    {
    }
}
