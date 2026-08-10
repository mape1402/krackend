using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.Design.Interaction;

/// <summary>
/// Represents disable trigger binding command.
/// </summary>
public sealed record DisableTriggerBindingCommand(string Id) : IRequest<bool>;

