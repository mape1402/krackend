using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.Design.Interaction;

/// <summary>
/// Represents get task definition by id query.
/// </summary>
public sealed record GetTaskDefinitionByIdQuery(string Id) : IRequest<TaskDefinitionModel>;

