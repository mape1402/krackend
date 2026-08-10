using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.Design.Interaction;

/// <summary>
/// Represents get stage definition by id query.
/// </summary>
public sealed record GetStageDefinitionByIdQuery(string Id) : IRequest<StageDefinitionModel>;

