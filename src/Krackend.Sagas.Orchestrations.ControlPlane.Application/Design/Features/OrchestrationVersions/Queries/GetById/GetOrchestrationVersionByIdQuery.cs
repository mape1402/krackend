using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Represents get orchestration version by id query.
/// </summary>
public sealed record GetOrchestrationVersionByIdQuery(string Id) : IRequest<OrchestrationVersionModel>;


