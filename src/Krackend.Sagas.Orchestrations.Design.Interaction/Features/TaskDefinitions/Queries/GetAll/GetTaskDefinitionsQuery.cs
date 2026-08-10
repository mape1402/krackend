using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.Design.Interaction;

/// <summary>
/// Represents get task definitions query.
/// </summary>
public sealed record GetTaskDefinitionsQuery(string StageDefinitionId) : IRequest<IEnumerable<TaskDefinitionModel>>;

