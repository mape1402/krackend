using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Artifacts;

namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Timeouts;

/// <summary>Resolves the exact response timeout deadline for a dispatched task without exposing artifact internals.</summary>
public interface ITaskResponseTimeoutDeadlineResolver
{
    /// <summary>Returns the task response deadline, or null when the task has no timeout policy.</summary>
    Task<DateTime?> ResolveAsync(
        Id orchestrationInstanceId,
        Id taskExecutionId,
        DateTime waitingSinceUtc,
        CancellationToken cancellationToken = default);
}

internal sealed class TaskResponseTimeoutDeadlineResolver(
    IOrchestrationInstanceRepository instances,
    ITaskExecutionRepository tasks,
    IStageExecutionRepository stages,
    IRuntimeArtifactResolver artifacts) : ITaskResponseTimeoutDeadlineResolver
{
    public async Task<DateTime?> ResolveAsync(
        Id orchestrationInstanceId,
        Id taskExecutionId,
        DateTime waitingSinceUtc,
        CancellationToken cancellationToken = default)
    {
        var instance = await instances.GetById(orchestrationInstanceId, cancellationToken);
        var task = await tasks.GetById(taskExecutionId, cancellationToken);
        if (task.OrchestrationInstanceId != instance.Id)
        {
            return null;
        }

        var stage = await stages.GetById(task.StageExecutionId, cancellationToken);
        var artifact = await artifacts.ResolveAsync(instance.RuntimeOrchestrationArtifactId.ToString(), cancellationToken);
        var timeout = artifact.Artifact.StageDefinitions
            .FirstOrDefault(x => string.Equals(x.Key, stage.StageKey, StringComparison.OrdinalIgnoreCase))?
            .TaskDefinitions
            .FirstOrDefault(x => string.Equals(x.Key, task.TaskKey, StringComparison.OrdinalIgnoreCase))?
            .TimeoutPolicy?
            .Timeout;

        return timeout is null ? null : waitingSinceUtc.Add(timeout.Value.Value);
    }
}
