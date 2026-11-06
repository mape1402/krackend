namespace Krackend.Sagas.Orchestrations.Tests.Runtime.Support;

using Krackend.Sagas.Orchestrations.Runtime.Distribution;

internal sealed class RecordingRuntimeArtifactProjectionScheduler : IRuntimeDurableRolloutProjectionScheduler
{
    public List<RuntimeArtifactProjectionRequest> Requests { get; } = [];

    public Task ScheduleProjectionAsync(
        RuntimeArtifactProjectionRequest request,
        CancellationToken cancellationToken = default)
    {
        Requests.Add(request);
        return Task.CompletedTask;
    }
}
