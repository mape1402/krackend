using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Represents get parallel group definition by id query.
/// </summary>
public sealed record GetParallelGroupDefinitionByIdQuery(string Id) : IRequest<ParallelGroupDefinitionModel>;

