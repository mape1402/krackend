using System.Text.Json;
using Krackend.Sagas.Orchestrations.Abstractions.Artifacts;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;

namespace Krackend.Sagas.Orchestrations.Runtime.Diagnostics;

/// <summary>
/// Produces the small, display-safe progress projection carried by a runtime
/// reactive event. It deliberately works only with the artifact already
/// available in the current unit of work; publishing a SignalR delta must not
/// issue additional storage queries.
/// </summary>
public static class RuntimeReactiveProgressCalculator
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Gets the enabled task progress for an instance when its artifact is
    /// already available locally. A null result means the caller must retain
    /// the last known display value instead of loading from storage.
    /// </summary>
    public static RuntimeReactiveProgress? TryCalculate(
        OrchestrationInstance instance,
        RuntimeOrchestrationArtifact runtimeArtifact)
    {
        if (instance is null || runtimeArtifact?.ArtifactPayload is null)
        {
            return null;
        }

        OrchestrationArtifact artifact;
        try
        {
            artifact = JsonSerializer.Deserialize<OrchestrationArtifact>(
                runtimeArtifact.ArtifactPayload.ToJsonString(),
                SerializerOptions);
        }
        catch (JsonException)
        {
            return null;
        }

        if (artifact is null)
        {
            return null;
        }

        var positions = (artifact.StageDefinitions ?? Array.Empty<StageArtifact>())
            .Where(stage => stage.IsEnabled)
            .OrderBy(stage => stage.Order)
            .SelectMany(stage => (stage.TaskDefinitions ?? Array.Empty<TaskArtifact>())
                .Where(task => task.IsEnabled)
                .OrderBy(task => task.Order)
                .Select(task => new TaskPosition(stage.Key, task.Key)))
            .ToArray();

        if (positions.Length == 0)
        {
            return new RuntimeReactiveProgress(0, 0);
        }

        if (IsResolved(instance.Status))
        {
            return new RuntimeReactiveProgress(positions.Length, positions.Length);
        }

        var current = Array.FindIndex(positions, position =>
            string.Equals(position.StageKey, instance.CurrentStageKey, StringComparison.OrdinalIgnoreCase)
            && string.Equals(position.TaskKey, instance.CurrentTaskKey, StringComparison.OrdinalIgnoreCase));
        return new RuntimeReactiveProgress(Math.Max(0, current), positions.Length);
    }

    private static bool IsResolved(OrchestrationInstanceStatus status)
        => status is OrchestrationInstanceStatus.Completed
            or OrchestrationInstanceStatus.Compensated
            or OrchestrationInstanceStatus.Failed
            or OrchestrationInstanceStatus.Stopped
            or OrchestrationInstanceStatus.Aborted
            or OrchestrationInstanceStatus.CompletedWithErrors
            or OrchestrationInstanceStatus.DeadLettered;

    private sealed record TaskPosition(string StageKey, string TaskKey);
}

/// <summary>
/// Compact progress values included in a realtime delta.
/// </summary>
public readonly record struct RuntimeReactiveProgress(int CompletedTaskCount, int TotalTaskCount);
