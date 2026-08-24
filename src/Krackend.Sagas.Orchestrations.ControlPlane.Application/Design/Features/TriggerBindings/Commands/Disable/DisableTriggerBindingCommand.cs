using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Represents disable trigger binding command.
/// </summary>
public sealed record DisableTriggerBindingCommand(string Id) : IRequest<bool>;

