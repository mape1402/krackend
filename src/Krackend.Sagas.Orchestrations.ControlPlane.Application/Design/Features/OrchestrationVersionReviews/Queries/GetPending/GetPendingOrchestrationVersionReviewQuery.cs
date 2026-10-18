namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

using Pelican.Mediator;

/// <summary>Gets the lifecycle review currently awaiting application for a version.</summary>
public sealed record GetPendingOrchestrationVersionReviewQuery(string OrchestrationVersionId) : IRequest<OrchestrationVersionReviewModel>;
