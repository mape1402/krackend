using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Represents deploy orchestration version command.
/// </summary>
public sealed record DeployOrchestrationVersionCommand(string Id, string UpdatedBy) : IRequest<bool>;


