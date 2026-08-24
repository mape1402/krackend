using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Represents enable task definition command.
/// </summary>
public sealed record EnableTaskDefinitionCommand(string Id) : IRequest<bool>;

