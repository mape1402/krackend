using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.Design.Interaction;

/// <summary>
/// Represents get branch rule definition by id query.
/// </summary>
public sealed record GetBranchRuleDefinitionByIdQuery(string Id) : IRequest<BranchRuleDefinitionModel>;

