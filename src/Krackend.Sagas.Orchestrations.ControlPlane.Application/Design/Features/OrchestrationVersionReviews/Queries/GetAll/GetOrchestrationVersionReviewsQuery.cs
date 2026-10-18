namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

using Pelican.Mediator;

/// <summary>Gets the immutable lifecycle review history for an orchestration version.</summary>
public sealed record GetOrchestrationVersionReviewsQuery(string OrchestrationVersionId)
    : IRequest<IReadOnlyCollection<OrchestrationVersionReviewModel>>;
