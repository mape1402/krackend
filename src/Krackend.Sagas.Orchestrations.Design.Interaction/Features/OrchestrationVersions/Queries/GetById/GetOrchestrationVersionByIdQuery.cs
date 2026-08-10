using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.Design.Interaction;

/// <summary>
/// Represents get orchestration version by id query.
/// </summary>
public sealed record GetOrchestrationVersionByIdQuery(string Id) : IRequest<OrchestrationVersionModel>;


