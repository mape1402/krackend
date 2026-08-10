using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.Design.Interaction;

/// <summary>
/// Represents delete variable definition command.
/// </summary>
public sealed record DeleteVariableDefinitionCommand(string Id) : IRequest<bool>;

