using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Represents get stage definition by id query.
/// </summary>
public sealed record GetStageDefinitionByIdQuery(string Id) : IRequest<StageDefinitionModel>;

