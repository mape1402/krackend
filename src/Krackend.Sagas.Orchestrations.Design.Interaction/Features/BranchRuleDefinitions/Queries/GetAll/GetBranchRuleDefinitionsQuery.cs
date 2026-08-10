using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.Design.Interaction;

/// <summary>
/// Represents get branch rule definitions query.
/// </summary>
public sealed record GetBranchRuleDefinitionsQuery(string StageDefinitionId) : IRequest<IEnumerable<BranchRuleDefinitionModel>>;

