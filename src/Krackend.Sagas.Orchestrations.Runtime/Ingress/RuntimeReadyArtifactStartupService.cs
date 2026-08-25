using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Replication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Krackend.Sagas.Orchestrations.Runtime.Ingress;

/// <summary>
/// Schedules local ingress standup for ready artifacts when a runtime replica starts.
/// </summary>
public sealed class RuntimeReadyArtifactStartupService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RuntimeOptions _runtimeOptions;
    private readonly IRuntimeReplicaIdentity _replicaIdentity;

    /// <summary>
    /// Initializes a new instance of the <see cref="RuntimeReadyArtifactStartupService"/> class.
    /// </summary>
    public RuntimeReadyArtifactStartupService(
        IServiceScopeFactory scopeFactory,
        IOptions<RuntimeOptions> runtimeOptions,
        IRuntimeReplicaIdentity replicaIdentity)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _runtimeOptions = runtimeOptions?.Value ?? throw new ArgumentNullException(nameof(runtimeOptions));
        _replicaIdentity = replicaIdentity ?? throw new ArgumentNullException(nameof(replicaIdentity));
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var artifactRepository = scope.ServiceProvider.GetRequiredService<IRuntimeArtifactRepository>();
        var standupScheduler = scope.ServiceProvider.GetRequiredService<IRuntimeIngressStandupScheduler>();

        var artifacts = await artifactRepository.GetReady(_runtimeOptions.EnvironmentKey, cancellationToken);
        foreach (var artifact in artifacts)
        {
            await standupScheduler.ScheduleStandupAsync(new RuntimeIngressStandupRequest
            {
                ArtifactId = artifact.Id.ToString(),
                IngressGeneration = artifact.IngressGeneration,
                Reason = $"startup:{_replicaIdentity.ReplicaId}",
                RequestedOnUtc = DateTime.UtcNow
            }, cancellationToken);
        }
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var ingressRegistry = scope.ServiceProvider.GetRequiredService<IIngressRegistry>();
        await ingressRegistry.ShutDownAllAsync(cancellationToken);
    }
}
