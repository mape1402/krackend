using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Represents disable task definition command.
/// </summary>
public sealed record DisableTaskDefinitionCommand(string Id) : IRequest<bool>;

