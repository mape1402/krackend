using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Represents approve orchestration version command.
/// </summary>
public sealed record ApproveOrchestrationVersionCommand(string Id, string ApprovedBy) : IRequest<bool>;


