using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.Design.Interaction;

/// <summary>
/// Represents activate orchestration definition command.
/// </summary>
public sealed record ActivateOrchestrationDefinitionCommand(string Id) : IRequest<bool>;


