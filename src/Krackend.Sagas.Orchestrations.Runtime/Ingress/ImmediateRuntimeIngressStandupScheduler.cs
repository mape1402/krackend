using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;

namespace Krackend.Sagas.Orchestrations.Runtime.Ingress;

/// <summary>
/// Runs local ingress standup immediately when no durable local scheduler is configured.
/// </summary>
internal sealed class ImmediateRuntimeIngressStandupScheduler : IRuntimeIngressStandupScheduler
{
    private readonly IIngressRegistry _ingressRegistry;
    private readonly IRuntimeArtifactRepository _artifacts;

    /// <summary>
    /// Initializes a new instance of the <see cref="ImmediateRuntimeIngressStandupScheduler"/> class.
    /// </summary>
    public ImmediateRuntimeIngressStandupScheduler(IIngressRegistry ingressRegistry, IRuntimeArtifactRepository artifacts = null)
    {
        _ingressRegistry = ingressRegistry ?? throw new ArgumentNullException(nameof(ingressRegistry));
        _artifacts = artifacts;
    }

    /// <inheritdoc />
    public async Task ScheduleStandupAsync(RuntimeIngressStandupRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
            await _ingressRegistry.StandUpOneAsync(request.ArtifactId, request.IngressGeneration, cancellationToken);
        else
        {
            if (_artifacts is null || _ingressRegistry is not IRefreshIngressRegistry refresh)
                throw new IngressStandupConfigurationException("The runtime cannot refresh ingress after rollback.");
            var artifact = await _artifacts.GetById(new Id(Ulid.Parse(request.ArtifactId)), cancellationToken);
            if (artifact.IngressGeneration == request.IngressGeneration && artifact.IsActive &&
                artifact.Status == RuntimeOrchestrationArtifactStatus.Ready)
                await refresh.RefreshOneAsync(request.ArtifactId, request.IngressGeneration, cancellationToken);
        }
    }
}
