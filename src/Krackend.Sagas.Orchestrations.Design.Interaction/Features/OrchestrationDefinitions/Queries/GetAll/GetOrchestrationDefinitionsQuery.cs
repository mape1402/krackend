using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.Design.Interaction;

/// <summary>
/// Represents get orchestration definitions query.
/// </summary>
public sealed record GetOrchestrationDefinitionsQuery(InteractionPagedSettings Settings) : IRequest<InteractionPagedResult<OrchestrationDefinitionModel>>;


