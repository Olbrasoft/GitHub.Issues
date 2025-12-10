using Olbrasoft.Mediation;

namespace Olbrasoft.GitHub.Issues.Business;

/// <summary>
/// Base class for business services with mediator support.
/// </summary>
public abstract class Service(IMediator mediator)
{
    protected IMediator Mediator { get; } = mediator;
}
