using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;
using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Represents update branch rule definition command.
/// </summary>
public sealed record UpdateBranchRuleDefinitionCommand(
    string Id,
    ElementType FromType,
    string FromId,
    ExecutionCondition Condition,
    ElementType NavigateToType,
    string NavigateToId) : IRequest<bool>;

