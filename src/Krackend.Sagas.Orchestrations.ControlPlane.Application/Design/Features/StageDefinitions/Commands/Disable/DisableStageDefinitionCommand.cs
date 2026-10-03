using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Represents disable stage definition command.
/// </summary>
public sealed record DisableStageDefinitionCommand(string Id) : IRequest<bool>;
