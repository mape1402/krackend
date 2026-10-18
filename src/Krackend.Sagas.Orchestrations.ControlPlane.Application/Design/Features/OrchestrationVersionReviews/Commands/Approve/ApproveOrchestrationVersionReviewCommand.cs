namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

using Pelican.Mediator;

/// <summary>Approves an orchestration version review without applying its transition.</summary>
public sealed record ApproveOrchestrationVersionReviewCommand(string ReviewId, string ApprovedBy) : IRequest<bool>;
