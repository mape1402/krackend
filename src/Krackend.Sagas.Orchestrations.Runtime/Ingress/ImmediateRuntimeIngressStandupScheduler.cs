namespace Krackend.Sagas.Orchestrations.Runtime.Ingress;

/// <summary>
/// Runs local ingress standup immediately when no durable local scheduler is configured.
/// </summary>
internal sealed class ImmediateRuntimeIngressStandupScheduler : IRuntimeIngressStandupScheduler
{
    private readonly IIngressRegistry _ingressRegistry;

    /// <summary>
    /// Initializes a new instance of the <see cref="ImmediateRuntimeIngressStandupScheduler"/> class.
    /// </summary>
    public ImmediateRuntimeIngressStandupScheduler(IIngressRegistry ingressRegistry)
    {
        _ingressRegistry = ingressRegistry ?? throw new ArgumentNullException(nameof(ingressRegistry));
    }

    /// <inheritdoc />
    public Task ScheduleStandupAsync(RuntimeIngressStandupRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return _ingressRegistry.StandUpOneAsync(request.ArtifactId, request.IngressGeneration, cancellationToken);
    }
}
