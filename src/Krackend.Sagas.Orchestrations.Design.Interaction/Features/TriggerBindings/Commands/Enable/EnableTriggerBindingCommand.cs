using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.Design.Interaction;

/// <summary>
/// Represents enable trigger binding command.
/// </summary>
public sealed record EnableTriggerBindingCommand(string Id) : IRequest<bool>;

