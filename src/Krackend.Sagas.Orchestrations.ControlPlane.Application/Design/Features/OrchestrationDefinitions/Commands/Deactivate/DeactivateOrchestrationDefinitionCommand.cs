using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Represents deactivate orchestration definition command.
/// </summary>
public sealed record DeactivateOrchestrationDefinitionCommand(string Id) : IRequest<bool>;


