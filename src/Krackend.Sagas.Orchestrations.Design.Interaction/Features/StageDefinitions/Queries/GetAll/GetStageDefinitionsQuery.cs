using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.Design.Interaction;

/// <summary>
/// Represents get stage definitions query.
/// </summary>
public sealed record GetStageDefinitionsQuery(string OrchestrationVersionId) : IRequest<IEnumerable<StageDefinitionModel>>;

