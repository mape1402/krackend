using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;
using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Represents set stage execution condition command.
/// </summary>
public sealed record SetStageExecutionConditionCommand(
    string Id,
    ExecutionCondition ExecutionCondition) : IRequest<bool>;
