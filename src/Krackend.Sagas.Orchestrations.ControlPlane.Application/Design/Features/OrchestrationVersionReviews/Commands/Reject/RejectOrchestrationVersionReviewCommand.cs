namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

using Pelican.Mediator;

/// <summary>Rejects a review and restores the reviewed version to its prior state.</summary>
public sealed record RejectOrchestrationVersionReviewCommand(string ReviewId, string RejectedBy, string Comment) : IRequest<bool>;
