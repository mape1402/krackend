using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;
using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Represents create branch rule definition command.
/// </summary>
public sealed record CreateBranchRuleDefinitionCommand(
    ElementType FromType,
    string FromId,
    ExecutionCondition Condition,
    ElementType NavigateToType,
    string NavigateToId) : IRequest<string>;

