using Krackend.Sagas.Orchestrations.Abstractions.Artifacts;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;

namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Timeouts;

internal static class InstanceLifetimePolicy
{
    public static void Project(OrchestrationInstance instance, OrchestrationArtifact artifact,
        OrchestrationTimeoutOptions options, DateTime anchorUtc)
    {
        var (maximum, idle) = Resolve(artifact, options);
        Project(instance, maximum, idle, anchorUtc);
    }

    public static (int Maximum, int Idle) Resolve(OrchestrationArtifact artifact, OrchestrationTimeoutOptions options)
    {
        var maximum = artifact.MaxInstanceDurationSeconds ?? options.DefaultInstanceLifetimeSeconds;
        var idle = artifact.IdleDurationSeconds ?? options.DefaultIdleDurationSeconds;
        if (maximum < 0 || idle <= 0) throw new InvalidOperationException("Invalid orchestration lifetime or idle duration.");
        return (maximum, idle);
    }

    public static void Project(OrchestrationInstance instance, int maximum, int idle, DateTime anchorUtc)
    {
        if (maximum < 0 || idle <= 0) throw new InvalidOperationException("Invalid orchestration lifetime or idle duration.");
        instance.MaxInstanceDurationSeconds = maximum;
        instance.IdleDurationSeconds = idle;
        ResetDeadline(instance, anchorUtc);
    }

    public static void ResetDeadline(OrchestrationInstance instance, DateTime anchorUtc)
    {
        if (instance.MaxInstanceDurationSeconds is not { } maximum)
            throw new InvalidOperationException("The orchestration instance lifetime policy has not been projected.");
        if (maximum < 0) throw new InvalidOperationException("The orchestration instance lifetime cannot be negative.");
        instance.ExpiresOnUtc = maximum == 0 ? null : anchorUtc.AddSeconds(maximum);
    }

    public static DateTime? ReadRecoveryAnchor(OrchestrationInstance instance)
    {
        if (instance.Metadata is null || !instance.Metadata.TryGetValue("Recovery.ReplayRequestedOnUtc", out var value) || value is null) return null;
        try { return value.GetValue<DateTime>(); }
        catch (Exception error) when (error is InvalidOperationException or FormatException) { return null; }
    }
}
