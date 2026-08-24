using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Represents delete variable definition command.
/// </summary>
public sealed record DeleteVariableDefinitionCommand(string Id) : IRequest<bool>;

