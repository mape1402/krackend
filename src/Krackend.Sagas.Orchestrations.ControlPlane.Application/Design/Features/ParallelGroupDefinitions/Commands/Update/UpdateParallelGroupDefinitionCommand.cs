using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Represents update parallel group definition command.
/// </summary>
public sealed record UpdateParallelGroupDefinitionCommand(
    string Id,
    string Name,
    ParallelJoinPolicy JoinPolicy,
    int? MaxParallelAgents) : IRequest<bool>;

