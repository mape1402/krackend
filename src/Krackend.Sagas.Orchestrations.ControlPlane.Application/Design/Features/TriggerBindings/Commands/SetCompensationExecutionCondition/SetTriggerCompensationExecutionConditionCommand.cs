using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;
using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Represents set trigger compensation execution condition command.
/// </summary>
public sealed record SetTriggerCompensationExecutionConditionCommand(
    string Id,
    ExecutionCondition ExecutionCondition) : IRequest<bool>;

