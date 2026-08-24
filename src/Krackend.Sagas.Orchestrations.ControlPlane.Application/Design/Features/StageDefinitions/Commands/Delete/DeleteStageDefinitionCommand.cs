using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Represents delete stage definition command.
/// </summary>
public sealed record DeleteStageDefinitionCommand(string Id) : IRequest<bool>;

