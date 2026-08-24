using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Represents get variable definition by id query.
/// </summary>
public sealed record GetVariableDefinitionByIdQuery(string Id) : IRequest<VariableDefinitionModel>;

