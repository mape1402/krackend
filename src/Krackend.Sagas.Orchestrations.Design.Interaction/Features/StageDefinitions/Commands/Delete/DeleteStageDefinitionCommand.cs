using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.Design.Interaction;

/// <summary>
/// Represents delete stage definition command.
/// </summary>
public sealed record DeleteStageDefinitionCommand(string Id) : IRequest<bool>;

