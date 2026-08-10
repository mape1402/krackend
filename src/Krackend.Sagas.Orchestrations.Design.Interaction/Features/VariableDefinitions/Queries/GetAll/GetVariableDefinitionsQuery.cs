using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.Design.Interaction;

/// <summary>
/// Represents get variable definitions query.
/// </summary>
public sealed record GetVariableDefinitionsQuery(string OrchestrationVersionId) : IRequest<IEnumerable<VariableDefinitionModel>>;

