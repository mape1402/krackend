using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using System.Text.Json.Nodes;

namespace Krackend.Sagas.Orchestrations.Engine;

/// <summary>
/// Describes a runtime execution transition before it is written to the timeline repository.
/// </summary>
internal sealed class RuntimeTransition
{
    /// <summary>
    /// Gets the orchestration instance that owns the transition.
    /// </summary>
    public required OrchestrationInstance Instance { get; init; }

    /// <summary>
    /// Gets the transition type written to the execution timeline.
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// Gets the status before the transition.
    /// </summary>
    public string FromStatus { get; init; }

    /// <summary>
    /// Gets the status after the transition.
    /// </summary>
    public string ToStatus { get; init; }

    /// <summary>
    /// Gets the related stage execution when the transition is stage-scoped.
    /// </summary>
    public StageExecution StageExecution { get; init; }

    /// <summary>
    /// Gets the related task execution when the transition is task-scoped.
    /// </summary>
    public TaskExecution TaskExecution { get; init; }

    /// <summary>
    /// Gets the related task attempt when the transition is attempt-scoped.
    /// </summary>
    public TaskExecutionAttempt Attempt { get; init; }

    /// <summary>
    /// Gets the optional transition payload.
    /// </summary>
    public JsonNode Payload { get; init; }

    /// <summary>
    /// Creates an instance-level transition.
    /// </summary>
    public static RuntimeTransition ForInstance<TFrom, TTo>(OrchestrationInstance instance, string type, TFrom fromStatus, TTo toStatus)
        => new() { Instance = instance, Type = type, FromStatus = fromStatus?.ToString(), ToStatus = toStatus?.ToString() };

    /// <summary>
    /// Creates a stage-level transition.
    /// </summary>
    public static RuntimeTransition ForStage<TFrom, TTo>(OrchestrationInstance instance, string type, TFrom fromStatus, TTo toStatus, StageExecution stageExecution)
        => new() { Instance = instance, Type = type, FromStatus = fromStatus?.ToString(), ToStatus = toStatus?.ToString(), StageExecution = stageExecution };

    /// <summary>
    /// Creates a task-level transition.
    /// </summary>
    public static RuntimeTransition ForTask<TFrom, TTo>(OrchestrationInstance instance, string type, TFrom fromStatus, TTo toStatus, StageExecution stageExecution, TaskExecution taskExecution)
        => new() { Instance = instance, Type = type, FromStatus = fromStatus?.ToString(), ToStatus = toStatus?.ToString(), StageExecution = stageExecution, TaskExecution = taskExecution };

    /// <summary>
    /// Creates an attempt-level transition.
    /// </summary>
    public static RuntimeTransition ForAttempt<TFrom, TTo>(OrchestrationInstance instance, string type, TFrom fromStatus, TTo toStatus, StageExecution stageExecution, TaskExecution taskExecution, TaskExecutionAttempt attempt)
        => new() { Instance = instance, Type = type, FromStatus = fromStatus?.ToString(), ToStatus = toStatus?.ToString(), StageExecution = stageExecution, TaskExecution = taskExecution, Attempt = attempt };

    /// <summary>
    /// Creates an attempt-level transition with a payload.
    /// </summary>
    public static RuntimeTransition ForAttemptPayload<TFrom, TTo>(OrchestrationInstance instance, string type, TFrom fromStatus, TTo toStatus, StageExecution stageExecution, TaskExecution taskExecution, TaskExecutionAttempt attempt, JsonNode payload)
        => new() { Instance = instance, Type = type, FromStatus = fromStatus?.ToString(), ToStatus = toStatus?.ToString(), StageExecution = stageExecution, TaskExecution = taskExecution, Attempt = attempt, Payload = payload };
}
