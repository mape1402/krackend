using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.Design.Interaction;

/// <summary>
/// Represents create parallel group definition command.
/// </summary>
public sealed record CreateParallelGroupDefinitionCommand(
    string Id,
    string StageDefinitionId,
    string Name,
    ParallelJoinPolicy JoinPolicy,
    int? MaxParallelAgents) : IRequest<string>;

