using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Represents enable trigger binding command.
/// </summary>
public sealed record EnableTriggerBindingCommand(string Id) : IRequest<bool>;

