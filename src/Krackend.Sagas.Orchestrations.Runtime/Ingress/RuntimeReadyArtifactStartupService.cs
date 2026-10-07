using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Operations;
using Krackend.Sagas.Orchestrations.Runtime.Replication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Krackend.Sagas.Orchestrations.Runtime.Ingress;

/// <summary>
/// Schedules local ingress standup for ready artifacts when a runtime replica starts.
/// </summary>
public sealed class RuntimeReadyArtifactStartupService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IIngressRegistry _ingressRegistry;
    private readonly IRuntimeReplicaIdentity _replicaIdentity;
    private readonly IRuntimeOperationalStateController _stateController;
    private readonly IRuntimeAdmissionController _admissionController;

    /// <summary>
    /// Initializes a new instance of the <see cref="RuntimeReadyArtifactStartupService"/> class.
    /// </summary>
    public RuntimeReadyArtifactStartupService(
        IServiceScopeFactory scopeFactory,
        IIngressRegistry ingressRegistry,
        IRuntimeReplicaIdentity replicaIdentity,
        IRuntimeOperationalStateController stateController,
        IRuntimeAdmissionController admissionController)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _ingressRegistry = ingressRegistry ?? throw new ArgumentNullException(nameof(ingressRegistry));
        _replicaIdentity = replicaIdentity ?? throw new ArgumentNullException(nameof(replicaIdentity));
        _stateController = stateController ?? throw new ArgumentNullException(nameof(stateController));
        _admissionController = admissionController ?? throw new ArgumentNullException(nameof(admissionController));
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var snapshot = await _stateController.EvaluateAsync(cancellationToken);
        if (snapshot.State == RuntimeOperationalState.Closed ||
            !_admissionController.CanAccept(RuntimeAdmissionOperation.IngressStandup))
        {
            return;
        }

        await using var scope = _scopeFactory.CreateAsyncScope();
        var artifactRepository = scope.ServiceProvider.GetRequiredService<IRuntimeArtifactRepository>();
        var standupScheduler = scope.ServiceProvider.GetRequiredService<IRuntimeIngressStandupScheduler>();

        var artifacts = await artifactRepository.GetReady(cancellationToken);
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
        await _ingressRegistry.ShutDownAllAsync(cancellationToken);
    }
}
