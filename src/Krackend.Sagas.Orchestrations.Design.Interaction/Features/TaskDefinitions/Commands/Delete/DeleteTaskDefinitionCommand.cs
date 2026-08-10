using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.Design.Interaction;

/// <summary>
/// Represents delete task definition command.
/// </summary>
public sealed record DeleteTaskDefinitionCommand(string Id) : IRequest<bool>;

