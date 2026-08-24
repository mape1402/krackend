using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Represents get task definitions query.
/// </summary>
public sealed record GetTaskDefinitionsQuery(string StageDefinitionId) : IRequest<IEnumerable<TaskDefinitionModel>>;

