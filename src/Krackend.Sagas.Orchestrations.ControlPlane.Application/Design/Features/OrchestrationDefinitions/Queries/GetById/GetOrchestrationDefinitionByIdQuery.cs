using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Represents get orchestration definition by id query.
/// </summary>
public sealed record GetOrchestrationDefinitionByIdQuery(string Id) : IRequest<OrchestrationDefinitionModel>;


