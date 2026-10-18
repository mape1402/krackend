namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

using Pelican.Mediator;

/// <summary>Aborts a pending or approved review before its transition is applied.</summary>
public sealed record AbortOrchestrationVersionReviewCommand(string ReviewId, string AbortedBy, string Reason) : IRequest<bool>;
