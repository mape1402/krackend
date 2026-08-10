using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.Design.Interaction;

/// <summary>
/// Represents delete parallel group definition command.
/// </summary>
public sealed record DeleteParallelGroupDefinitionCommand(string Id) : IRequest<bool>;

