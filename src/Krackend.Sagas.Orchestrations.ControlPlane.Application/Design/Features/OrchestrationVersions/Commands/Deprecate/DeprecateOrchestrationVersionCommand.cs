using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Represents deprecate orchestration version command.
/// </summary>
public sealed record DeprecateOrchestrationVersionCommand(string Id, string UpdatedBy) : IRequest<bool>;


