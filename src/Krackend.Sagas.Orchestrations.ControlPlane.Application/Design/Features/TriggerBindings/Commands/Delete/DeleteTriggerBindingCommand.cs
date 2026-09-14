using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Represents delete trigger binding command.
/// </summary>
public sealed record DeleteTriggerBindingCommand(string Id) : IRequest<bool>;

