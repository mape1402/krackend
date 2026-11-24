using System.Security.Cryptography;
using System.Text;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Distribution;
using Krackend.Sagas.Orchestrations.Runtime.Ingress;
using Krackend.Sagas.Orchestrations.Runtime.Replication;
using Microsoft.Extensions.Logging;
using Mule;

namespace Krackend.Sagas.Orchestrations.Runtime.Buffering.Mule;

/// <summary>
/// Executes replica-local ingress standup directly and uses Mule only as the
/// durable retry path when a transient connector failure occurs.
/// </summary>
internal sealed class MuleRuntimeIngressStandupScheduler(
    IMuleClient client,
    IRuntimeReplicaIdentity replicaIdentity,
    IIngressRegistry ingressRegistry,
    IRuntimeArtifactRepository artifacts,
    ILogger<MuleRuntimeIngressStandupScheduler> logger) : IRuntimeIngressStandupScheduler
{
    public async Task ScheduleStandupAsync(
        RuntimeIngressStandupRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            if (string.IsNullOrWhiteSpace(request.RefreshToken))
                await ingressRegistry.StandUpOneAsync(request.ArtifactId, request.IngressGeneration, cancellationToken);
            else
            {
                var artifact = await artifacts.GetById(
                    new Id(System.Ulid.Parse(request.ArtifactId)), cancellationToken);
                if (artifact.IngressGeneration != request.IngressGeneration || !artifact.IsActive ||
                    artifact.Status != RuntimeOrchestrationArtifactStatus.Ready) return;
                if (ingressRegistry is not IRefreshIngressRegistry refresh)
                    throw new IngressStandupConfigurationException(
                        "The runtime cannot refresh ingress after rollback.");
                await refresh.RefreshOneAsync(request.ArtifactId, request.IngressGeneration, cancellationToken);
            }
            return;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (IngressStandupConfigurationException exception)
        {
            if (string.IsNullOrWhiteSpace(request.RefreshToken))
            {
                await artifacts.MarkProjectionFailed(
                    new Id(System.Ulid.Parse(request.ArtifactId)),
                    request.IngressGeneration,
                    exception.Message,
                    cancellationToken);
            }
            logger.LogWarning(exception,
                "Ingress standup for artifact {ArtifactId} generation {IngressGeneration} failed permanently.",
                request.ArtifactId, request.IngressGeneration);
            return;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception,
                "Direct ingress standup failed for artifact {ArtifactId}; scheduling a durable Mule retry.",
                request.ArtifactId);
        }

        var deduplicationKey =
            $"{request.ArtifactId}:{request.IngressGeneration}:{replicaIdentity.ReplicaBootId}";
        if (!string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            var input = $"{deduplicationKey}:{request.RefreshToken}";
            deduplicationKey =
                $"refresh:{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input)))}";
        }

        await client.EnqueueAsync(
            ActionKey.From(RuntimeArtifactActionNames.StandUpArtifactIngress),
            request,
            options =>
            {
                options.Lane = replicaIdentity.LocalStandupLane;
                options.CorrelationId = request.ArtifactId;
                options.DeduplicationKey = deduplicationKey;
                options.Metadata["replicaId"] = replicaIdentity.ReplicaId;
                options.Metadata["replicaBootId"] = replicaIdentity.ReplicaBootId;
                options.Metadata["reason"] = request.Reason ?? string.Empty;
                options.Metadata["ingressGeneration"] = request.IngressGeneration.ToString();
            },
            cancellationToken);
    }
}
