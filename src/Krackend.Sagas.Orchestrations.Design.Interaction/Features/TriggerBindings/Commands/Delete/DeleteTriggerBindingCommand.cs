using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.Design.Interaction;

/// <summary>
/// Represents delete trigger binding command.
/// </summary>
public sealed record DeleteTriggerBindingCommand(string Id) : IRequest<bool>;

