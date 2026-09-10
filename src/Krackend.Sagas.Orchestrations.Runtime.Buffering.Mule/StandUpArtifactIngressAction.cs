using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Distribution;
using Krackend.Sagas.Orchestrations.Runtime.Ingress;
using Krackend.Sagas.Orchestrations.Runtime.Replication;
using Microsoft.Extensions.Logging;
using Mule;

namespace Krackend.Sagas.Orchestrations.Runtime.Buffering.Mule;

/// <summary>
/// Stands up ready ingress configuration inside the current runtime replica.
/// </summary>
[MuleAction(RuntimeArtifactActionNames.StandUpArtifactIngress)]
public sealed class StandUpArtifactIngressAction : IMuleAction<RuntimeIngressStandupRequest>
{
    private readonly IRuntimeArtifactRepository _artifactRepository;
    private readonly IIngressRegistry _ingressRegistry;
    private readonly IRuntimeReplicaIdentity _replicaIdentity;
    private readonly ILogger<StandUpArtifactIngressAction> _logger;
    private readonly IMuleTerminalFailureMarker _terminalFailureMarker;

    /// <summary>
    /// Initializes a new instance of the <see cref="StandUpArtifactIngressAction"/> class.
    /// </summary>
    public StandUpArtifactIngressAction(
        IRuntimeArtifactRepository artifactRepository,
        IIngressRegistry ingressRegistry,
        IRuntimeReplicaIdentity replicaIdentity,
        ILogger<StandUpArtifactIngressAction> logger,
        IMuleTerminalFailureMarker terminalFailureMarker)
    {
        _artifactRepository = artifactRepository ?? throw new ArgumentNullException(nameof(artifactRepository));
        _ingressRegistry = ingressRegistry ?? throw new ArgumentNullException(nameof(ingressRegistry));
        _replicaIdentity = replicaIdentity ?? throw new ArgumentNullException(nameof(replicaIdentity));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _terminalFailureMarker = terminalFailureMarker ?? throw new ArgumentNullException(nameof(terminalFailureMarker));
    }

    /// <inheritdoc />
    public async ValueTask ExecuteAsync(
        MuleActionContext<RuntimeIngressStandupRequest> context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!string.Equals(context.Lane, _replicaIdentity.LocalStandupLane, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogDebug(
                "Skipping ingress standup action {ActionId} for lane {Lane}; current replica lane is {ReplicaLane}.",
                context.Id,
                context.Lane,
                _replicaIdentity.LocalStandupLane);
            return;
        }

        var request = context.Payload ?? throw new InvalidOperationException("Ingress standup request payload is required.");
        try
        {
            await _ingressRegistry.StandUpOneAsync(request.ArtifactId, request.IngressGeneration, cancellationToken);
        }
        catch (IngressStandupConfigurationException exception)
        {
            await _artifactRepository.MarkProjectionFailed(
                new Id(Ulid.Parse(request.ArtifactId)),
                request.IngressGeneration,
                exception.Message,
                cancellationToken);

            _logger.LogWarning(
                exception,
                "Ingress standup for artifact {ArtifactId} generation {IngressGeneration} failed permanently because runtime configuration is incomplete.",
                request.ArtifactId,
                request.IngressGeneration);

            _terminalFailureMarker.MarkTerminal(context);
            throw;
        }
    }
}
