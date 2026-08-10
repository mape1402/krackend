using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.Design.Interaction;

/// <summary>
/// Represents get parallel group definitions query.
/// </summary>
public sealed record GetParallelGroupDefinitionsQuery(string StageDefinitionId) : IRequest<IEnumerable<ParallelGroupDefinitionModel>>;

