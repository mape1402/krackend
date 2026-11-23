using System.Security.Cryptography;
using System.Text;
using Krackend.Sagas.Orchestrations.Runtime.Distribution;
using Krackend.Sagas.Orchestrations.Runtime.Ingress;
using Krackend.Sagas.Orchestrations.Runtime.Replication;
using Mule;

namespace Krackend.Sagas.Orchestrations.Runtime.Buffering.Mule;

/// <summary>
/// Schedules replica-local ingress standup through the configured Mule provider.
/// The scheduler must not write provider-specific durable-action tables directly.
/// </summary>
internal sealed class MuleRuntimeIngressStandupScheduler(
    IMuleClient client,
    IRuntimeReplicaIdentity replicaIdentity) : IRuntimeIngressStandupScheduler
{
    public async Task ScheduleStandupAsync(
        RuntimeIngressStandupRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

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
