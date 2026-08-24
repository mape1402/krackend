using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Represents delete task definition command.
/// </summary>
public sealed record DeleteTaskDefinitionCommand(string Id) : IRequest<bool>;

