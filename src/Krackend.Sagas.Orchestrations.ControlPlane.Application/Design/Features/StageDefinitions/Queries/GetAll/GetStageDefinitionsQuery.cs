using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Represents get stage definitions query.
/// </summary>
public sealed record GetStageDefinitionsQuery(string OrchestrationVersionId) : IRequest<IEnumerable<StageDefinitionModel>>;

