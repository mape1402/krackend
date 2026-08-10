using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.Design.Interaction;

/// <summary>
/// Represents deploy orchestration version command.
/// </summary>
public sealed record DeployOrchestrationVersionCommand(string Id, string UpdatedBy) : IRequest<bool>;


