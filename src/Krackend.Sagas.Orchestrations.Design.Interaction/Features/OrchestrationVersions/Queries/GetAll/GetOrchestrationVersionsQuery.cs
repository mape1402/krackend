using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.Design.Interaction;

/// <summary>
/// Represents get orchestration versions query.
/// </summary>
public sealed record GetOrchestrationVersionsQuery(string OrchestrationDefinitionId, InteractionPagedSettings Settings)
    : IRequest<InteractionPagedResult<OrchestrationVersionModel>>;


