using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Represents get orchestration versions query.
/// </summary>
public sealed record GetOrchestrationVersionsQuery(string OrchestrationDefinitionId, ApplicationPagedSettings Settings)
    : IRequest<ApplicationPagedResult<OrchestrationVersionModel>>;


