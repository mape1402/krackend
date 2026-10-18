namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

using Pelican.Mediator;

/// <summary>Gets lifecycle reviews across all orchestration versions.</summary>
public sealed record GetAllOrchestrationVersionReviewsQuery(ApplicationPagedSettings Settings)
    : IRequest<ApplicationPagedResult<OrchestrationVersionReviewModel>>;
