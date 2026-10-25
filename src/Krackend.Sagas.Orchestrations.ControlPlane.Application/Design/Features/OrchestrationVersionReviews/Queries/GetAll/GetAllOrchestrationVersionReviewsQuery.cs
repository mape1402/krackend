namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

using Pelican.Mediator;

/// <summary>Gets lifecycle reviews across all orchestration versions.</summary>
public sealed record GetAllOrchestrationVersionReviewsQuery(ApplicationPagedSettings Settings)
    : IRequest<ApplicationPagedResult<OrchestrationVersionReviewModel>>
{
    /// <summary>Text to search in review and orchestration fields.</summary>
    public string SearchText { get; init; }

    /// <summary>Catalog sort field.</summary>
    public string SortField { get; init; }

    /// <summary>Whether to sort descending.</summary>
    public bool Descending { get; init; }
}
