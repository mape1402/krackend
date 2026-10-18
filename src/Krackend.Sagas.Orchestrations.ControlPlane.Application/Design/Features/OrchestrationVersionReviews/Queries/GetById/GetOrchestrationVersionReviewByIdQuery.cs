namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

using Pelican.Mediator;

/// <summary>Gets one independent orchestration version review.</summary>
public sealed record GetOrchestrationVersionReviewByIdQuery(string ReviewId) : IRequest<OrchestrationVersionReviewModel>;
