using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Represents get variable definitions query.
/// </summary>
public sealed record GetVariableDefinitionsQuery(string OrchestrationVersionId) : IRequest<IEnumerable<VariableDefinitionModel>>;

