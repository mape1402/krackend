using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Represents enable stage definition command.
/// </summary>
public sealed record EnableStageDefinitionCommand(string Id) : IRequest<bool>;
