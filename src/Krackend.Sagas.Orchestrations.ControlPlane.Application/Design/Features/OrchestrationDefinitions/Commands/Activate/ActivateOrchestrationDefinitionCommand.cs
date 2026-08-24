using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Represents activate orchestration definition command.
/// </summary>
public sealed record ActivateOrchestrationDefinitionCommand(string Id) : IRequest<bool>;


