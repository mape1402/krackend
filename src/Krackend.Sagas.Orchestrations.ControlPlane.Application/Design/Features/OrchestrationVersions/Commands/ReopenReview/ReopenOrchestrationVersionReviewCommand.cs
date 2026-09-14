using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Represents reopen orchestration version review command.
/// </summary>
public sealed record ReopenOrchestrationVersionReviewCommand(string Id, string UpdatedBy) : IRequest<bool>;


