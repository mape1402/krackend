using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.Design.Interaction;

/// <summary>
/// Represents deactivate orchestration definition command.
/// </summary>
public sealed record DeactivateOrchestrationDefinitionCommand(string Id) : IRequest<bool>;


