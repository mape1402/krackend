using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Intake;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Reactive;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;

namespace Krackend.Sagas.Orchestrations.Engine;

/// <summary>
/// Dependency set required by <see cref="RuntimeEngine"/>.
/// </summary>
public sealed class RuntimeEngineDependencies
{
    /// <summary>
    /// Gets the trigger intake buffer.
    /// </summary>
    public required ITriggerIntakeBuffer IntakeBuffer { get; init; }

    /// <summary>
    /// Gets the service that promotes intake entries into runtime instances.
    /// </summary>
    public required ITriggerPromoter TriggerPromoter { get; init; }

    /// <summary>
    /// Gets the runtime artifact repository.
    /// </summary>
    public required IRuntimeArtifactRepository ArtifactRepository { get; init; }

    /// <summary>
    /// Gets the stage execution repository.
    /// </summary>
    public required IStageExecutionRepository StageRepository { get; init; }

    /// <summary>
    /// Gets the task execution repository.
    /// </summary>
    public required ITaskExecutionRepository TaskRepository { get; init; }

    /// <summary>
    /// Gets the task attempt repository.
    /// </summary>
    public required ITaskExecutionAttemptRepository AttemptRepository { get; init; }

    /// <summary>
    /// Gets the task dispatch repository.
    /// </summary>
    public required ITaskDispatchRepository DispatchRepository { get; init; }

    /// <summary>
    /// Gets the orchestration instance repository.
    /// </summary>
    public required IOrchestrationInstanceRepository InstanceRepository { get; init; }

    /// <summary>
    /// Gets the execution timeline repository.
    /// </summary>
    public required IExecutionTransitionRepository TimelineRepository { get; init; }

    /// <summary>
    /// Gets the runtime task dispatcher resolver.
    /// </summary>
    public required IRuntimeTaskDispatcherResolver TaskDispatcherResolver { get; init; }

    /// <summary>
    /// Gets the runtime condition evaluator.
    /// </summary>
    public required IRuntimeConditionEvaluator ConditionEvaluator { get; init; }

    /// <summary>
    /// Gets the reactive event publisher.
    /// </summary>
    public required IRuntimeReactiveEventPublisher ReactiveEventPublisher { get; init; }
}
