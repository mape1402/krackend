using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;
using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Represents update task definition command.
/// </summary>
public sealed record UpdateTaskDefinitionCommand(
    string Id,
    string Key,
    string Name,
    int Order,
    string Notes,
    TaskKind Kind,
    TaskExecutionMode ExecutionMode,
    string ParallelGroupId,
    ExecutionCondition ExecutionCondition,
    TransformationDefinition Transformation,
    ITaskConfiguration Configuration,
    RetryPolicy RetryPolicy,
    TimeoutPolicy TimeoutPolicy,
    OnErrorPolicy OnErrorPolicy,
    CompensationDefinition CompensationDefinition,
    TaskDispatchType DispatchType,
    bool IsEnabled) : IRequest<bool>;

