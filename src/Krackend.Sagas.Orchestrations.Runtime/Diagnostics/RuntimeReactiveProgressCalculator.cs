using Krackend.Sagas.Orchestrations.Abstractions.Runtime;

namespace Krackend.Sagas.Orchestrations.Runtime.Diagnostics;

/// <summary>Returns the persisted progress projection without loading artifact payloads.</summary>
public static class RuntimeReactiveProgressCalculator
{
    /// <summary>
    /// Returns null when the instance has no trustworthy projection, for example
    /// an older instance awaiting backfill. The artifact argument is retained for
    /// compatibility with existing callers and is not read.
    /// </summary>
    public static RuntimeReactiveProgress? TryCalculate(
        OrchestrationInstance instance,
        RuntimeOrchestrationArtifact runtimeArtifact)
    {
        if (instance?.CompletedTaskCount is not { } completed || instance.TotalTaskCount is not { } total)
            return null;
        if (completed < 0 || total < 0 || completed > total)
            return null;

        return new RuntimeReactiveProgress(completed, total);
    }
}

/// <summary>Compact progress values included in a realtime delta.</summary>
public readonly record struct RuntimeReactiveProgress(int CompletedTaskCount, int TotalTaskCount);
