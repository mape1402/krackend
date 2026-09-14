using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Represents return orchestration version to draft command.
/// </summary>
public sealed record ReturnOrchestrationVersionToDraftCommand(string Id) : IRequest<bool>;


