using System.Security.Cryptography;
using System.Text;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Recovery;
using Mule;
using StackExchange.Redis;

namespace Krackend.Demo.RuntimeHost;

/// <summary>
/// Reads recovery state from the same authoritative Redis store used by Mule.
/// This prevents SQL and Redis from presenting conflicting action state.
/// </summary>
internal sealed class RedisOrchestrationDurableActionStateReader(
    IConnectionMultiplexer connection,
    IConfiguration configuration) : IOrchestrationDurableActionStateReader
{
    private readonly string _root = BuildRoot(ResolveKeyPrefix(
        configuration,
        string.Equals(configuration["Demo:RuntimeProvider"], "PostgreSql", StringComparison.OrdinalIgnoreCase)));

    internal static string ResolveKeyPrefix(IConfiguration configuration, bool usePostgres)
        => configuration["Demo:MuleKeyPrefix"] ??
           $"krackend:demo:runtime:{(usePostgres ? "postgresql" : "sqlserver")}:v3";

    private static string BuildRoot(string keyPrefix)
        => $"{{mule-{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(keyPrefix))).ToLowerInvariant()[..16]}}}:mule";

    public async Task<DurableWorkPresence> ReadAsync(
        string actionId,
        CancellationToken cancellationToken = default)
    {
        if (!global::Mule.Ulid.TryParse(actionId, out _))
            return DurableWorkPresence.Unknown;

        var status = await ReadStatusAsync(actionId, cancellationToken);
        return status switch
        {
            DurableActionStatus.Pending or DurableActionStatus.Locked => DurableWorkPresence.Present,
            DurableActionStatus.Completed or DurableActionStatus.Failed or DurableActionStatus.Cancelled =>
                DurableWorkPresence.Terminal,
            _ => DurableWorkPresence.Unknown
        };
    }

    public async Task<DurableWorkPresence> ReadPendingBeforeAsync(
        string actionId,
        DateTime deadlineUtc,
        CancellationToken cancellationToken = default)
    {
        if (!global::Mule.Ulid.TryParse(actionId, out _))
            return DurableWorkPresence.Unknown;

        cancellationToken.ThrowIfCancellationRequested();
        var values = await connection.GetDatabase().HashGetAsync(
            ActionKey(actionId),
            ["status", "created"]);
        cancellationToken.ThrowIfCancellationRequested();

        if (values.Length != 2 || values[0].IsNull || values[1].IsNull ||
            !int.TryParse(values[0].ToString(), out var rawStatus) ||
            !long.TryParse(values[1].ToString(), out var createdMilliseconds))
            return DurableWorkPresence.Unknown;

        var createdUtc = DateTimeOffset.FromUnixTimeMilliseconds(createdMilliseconds).UtcDateTime;
        if (createdUtc > DateTime.SpecifyKind(deadlineUtc, DateTimeKind.Utc))
            return DurableWorkPresence.Terminal;

        var status = (DurableActionStatus)rawStatus;
        return status is DurableActionStatus.Pending or DurableActionStatus.Locked
            ? DurableWorkPresence.Present
            : DurableWorkPresence.Terminal;
    }

    private async Task<DurableActionStatus?> ReadStatusAsync(
        string actionId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var value = await connection.GetDatabase().HashGetAsync(ActionKey(actionId), "status");
        cancellationToken.ThrowIfCancellationRequested();
        return value.IsNull || !int.TryParse(value.ToString(), out var status)
            ? null
            : (DurableActionStatus)status;
    }

    private RedisKey ActionKey(string actionId) => $"{_root}:action:{actionId}";
}
