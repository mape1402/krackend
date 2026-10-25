using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Represents get orchestration definitions query.
/// </summary>
public sealed record GetOrchestrationDefinitionsQuery(ApplicationPagedSettings Settings, string SearchText = "") : IRequest<ApplicationPagedResult<OrchestrationDefinitionModel>>;


