namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

using Pelican.Mediator;

/// <summary>Abandons a draft that has not been sent to review.</summary>
public sealed record AbandonOrchestrationVersionCommand(string Id, string AbandonedBy) : IRequest<bool>;
