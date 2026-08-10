using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.Design.Interaction;

/// <summary>
/// Represents get orchestration definition by id query.
/// </summary>
public sealed record GetOrchestrationDefinitionByIdQuery(string Id) : IRequest<OrchestrationDefinitionModel>;


