using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.Design.Interaction;

/// <summary>
/// Represents get parallel group definition by id query.
/// </summary>
public sealed record GetParallelGroupDefinitionByIdQuery(string Id) : IRequest<ParallelGroupDefinitionModel>;

