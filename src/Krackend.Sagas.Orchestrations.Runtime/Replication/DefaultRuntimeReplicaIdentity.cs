using Microsoft.Extensions.Options;

namespace Krackend.Sagas.Orchestrations.Runtime.Replication;

/// <summary>
/// Default runtime replica identity based on configuration or process environment.
/// </summary>
internal sealed class DefaultRuntimeReplicaIdentity : IRuntimeReplicaIdentity
{
    private readonly string _replicaId;
    private readonly string _replicaBootId;
    private readonly string _localStandupLane;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultRuntimeReplicaIdentity"/> class.
    /// </summary>
    public DefaultRuntimeReplicaIdentity(IOptions<RuntimeReplicaOptions> options)
    {
        var value = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _replicaId = NormalizeReplicaId(ResolveReplicaId(value));
        _replicaBootId = $"{_replicaId}-{Guid.NewGuid():N}";
        _localStandupLane = $"{NormalizeLanePart(value.StandupLanePrefix, "runtime-standup")}:{_replicaId}";
    }

    /// <inheritdoc />
    public string ReplicaId => _replicaId;

    /// <inheritdoc />
    public string ReplicaBootId => _replicaBootId;

    /// <inheritdoc />
    public string LocalStandupLane => _localStandupLane;

    private static string ResolveReplicaId(RuntimeReplicaOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.ReplicaId))
        {
            return options.ReplicaId;
        }

        var podUid = Environment.GetEnvironmentVariable("POD_UID");
        if (!string.IsNullOrWhiteSpace(podUid))
        {
            return podUid;
        }

        var kubernetesPodUid = Environment.GetEnvironmentVariable("KUBERNETES_POD_UID");
        if (!string.IsNullOrWhiteSpace(kubernetesPodUid))
        {
            return kubernetesPodUid;
        }

        var hostname = Environment.GetEnvironmentVariable("HOSTNAME");
        if (!string.IsNullOrWhiteSpace(hostname))
        {
            return $"{hostname}-{Environment.ProcessId}";
        }

        return $"{Environment.MachineName}-{Environment.ProcessId}";
    }

    private static string NormalizeReplicaId(string value)
        => NormalizeLanePart(value, "runtime-replica");

    private static string NormalizeLanePart(string value, string fallback)
    {
        var source = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        var chars = source.Select(character =>
            char.IsLetterOrDigit(character) || character is '-' or '_' or '.'
                ? char.ToLowerInvariant(character)
                : '-');

        var normalized = new string(chars.ToArray()).Trim('-');
        return string.IsNullOrWhiteSpace(normalized) ? fallback : normalized;
    }
}
