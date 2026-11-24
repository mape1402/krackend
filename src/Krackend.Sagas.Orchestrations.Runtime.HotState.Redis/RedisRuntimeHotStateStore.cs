#nullable enable

namespace Krackend.Sagas.Orchestrations.Runtime.HotState.Redis;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using StackExchange.Redis;

internal sealed class RedisRuntimeHotStateStore : IRuntimeHotStateStore, IAsyncDisposable
{
    private const string CommitScript = """
        local batch = cjson.decode(ARGV[1])
        for _, mutation in ipairs(batch) do
            if type(mutation.entityType) ~= 'string' or
               type(mutation.entityId) ~= 'string' or
               type(mutation.payload) ~= 'string' or
               (type(mutation.instanceId) ~= 'string' and type(mutation.instanceId) ~= 'userdata') or
               (type(mutation.indexes) ~= 'table' and type(mutation.indexes) ~= 'userdata') then
                return redis.error_reply('invalid runtime hot-state mutation')
            end
            if mutation.entityType == ARGV[3] and type(mutation.indexes) == 'table' and
               type(mutation.indexes.start) == 'string' and mutation.indexes.start ~= '' then
                local digest = redis.sha1hex(mutation.entityType .. '|start|' .. mutation.indexes.start)
                local owner = redis.call('GET', ARGV[2] .. ':lookup:' .. digest)
                if owner and owner ~= mutation.entityId then
                    return redis.error_reply('duplicate runtime start idempotency key')
                end
            end
        end
        local sequence = redis.call('INCR', KEYS[1])
        redis.call('HSET', KEYS[2], tostring(sequence), ARGV[1])
        redis.call('ZADD', KEYS[3], sequence, tostring(sequence))
        for _, mutation in ipairs(batch) do
            local entityKey = ARGV[2] .. ':entity:' .. mutation.entityType .. ':' .. mutation.entityId
            local current = tonumber(redis.call('HGET', entityKey, 'sequence') or '-1')
            if sequence >= current then
                redis.call('HSET', entityKey,
                    'entityType', mutation.entityType,
                    'entityId', mutation.entityId,
                    'instanceId', type(mutation.instanceId) == 'string' and mutation.instanceId or '',
                    'operation', mutation.operation,
                    'payload', mutation.payload,
                    'sequence', sequence)
                if type(mutation.instanceId) == 'string' and mutation.instanceId ~= '' then
                    redis.call('SADD', ARGV[2] .. ':owner:' .. mutation.instanceId .. ':' .. mutation.entityType, mutation.entityId)
                    redis.call('SADD', ARGV[2] .. ':owner-types:' .. mutation.instanceId, mutation.entityType)
                end
                if type(mutation.indexes) == 'table' then
                    for name, value in pairs(mutation.indexes) do
                        local digest = redis.sha1hex(mutation.entityType .. '|' .. name .. '|' .. value)
                        local lookupKey = ARGV[2] .. ':lookup:' .. digest
                        redis.call('SET', lookupKey, mutation.entityId)
                        if type(mutation.instanceId) == 'string' and mutation.instanceId ~= '' then
                            redis.call('SADD', ARGV[2] .. ':owner-lookups:' .. mutation.instanceId, lookupKey)
                        end
                    end
                end
            end
            if mutation.entityType == ARGV[3] then
                redis.call('ZADD', KEYS[4], sequence, mutation.entityId)
            end
        end
        return sequence
        """;

    private const string CacheScript = """
        local mutation = cjson.decode(ARGV[1])
        local sequence = tonumber(redis.call('GET', KEYS[1]) or '0')
        local entityKey = ARGV[2] .. ':entity:' .. mutation.entityType .. ':' .. mutation.entityId
        local current = tonumber(redis.call('HGET', entityKey, 'sequence') or '-1')
        if current > sequence then return 0 end
        redis.call('HSET', entityKey,
            'entityType', mutation.entityType,
            'entityId', mutation.entityId,
            'instanceId', type(mutation.instanceId) == 'string' and mutation.instanceId or '',
            'operation', mutation.operation,
            'payload', mutation.payload,
            'sequence', sequence)
        if type(mutation.instanceId) == 'string' and mutation.instanceId ~= '' then
            redis.call('SADD', ARGV[2] .. ':owner:' .. mutation.instanceId .. ':' .. mutation.entityType, mutation.entityId)
            redis.call('SADD', ARGV[2] .. ':owner-types:' .. mutation.instanceId, mutation.entityType)
        end
        if type(mutation.indexes) == 'table' then
            for name, value in pairs(mutation.indexes) do
                local digest = redis.sha1hex(mutation.entityType .. '|' .. name .. '|' .. value)
                local lookupKey = ARGV[2] .. ':lookup:' .. digest
                redis.call('SET', lookupKey, mutation.entityId)
                if type(mutation.instanceId) == 'string' and mutation.instanceId ~= '' then
                    redis.call('SADD', ARGV[2] .. ':owner-lookups:' .. mutation.instanceId, lookupKey)
                end
            end
        end
        if mutation.entityType == ARGV[3] then
            local instance = cjson.decode(mutation.payload)
            local status = tonumber(instance.status)
            if status == 4 or status == 6 or status == 7 or status == 8 or
               status == 9 or status == 10 or status == 11 then
                redis.call('ZADD', KEYS[2], sequence, mutation.entityId)
            end
        end
        return 1
        """;

    private const string AcquireLeaseScript = """
        local current = redis.call('GET', KEYS[1])
        if (not current) or current == ARGV[1] then
            redis.call('SET', KEYS[1], ARGV[1], 'PX', ARGV[2])
            return 1
        end
        return 0
        """;

    private const string ReleaseLeaseScript = """
        if redis.call('GET', KEYS[1]) == ARGV[1] then
            return redis.call('DEL', KEYS[1])
        end
        return 0
        """;

    private const string AcknowledgeScript = """
        if redis.call('GET', KEYS[1]) ~= ARGV[1] then return 0 end
        local sequenceCount = tonumber(ARGV[2])
        local maximum = tonumber(redis.call('GET', KEYS[4]) or '0')
        for index = 1, sequenceCount do
            local value = ARGV[index + 2]
            local sequence = tonumber(value)
            redis.call('ZREM', KEYS[2], value)
            redis.call('HDEL', KEYS[3], value)
            if sequence > maximum then maximum = sequence end
        end
        redis.call('SET', KEYS[4], maximum)
        redis.call('ZREMRANGEBYSCORE', KEYS[5], '-inf', maximum)
        local terminalCountIndex = sequenceCount + 3
        local terminalCount = tonumber(ARGV[terminalCountIndex])
        for index = 1, terminalCount do
            redis.call('ZADD', KEYS[6], maximum, ARGV[terminalCountIndex + index])
        end
        redis.call('DEL', KEYS[1])
        return maximum
        """;

    private const string CleanupProjectedTerminalScript = """
        local candidates = redis.call('ZRANGE', KEYS[1], 0, tonumber(ARGV[2]) - 1)
        local removed = 0
        for _, instanceId in ipairs(candidates) do
            if not redis.call('ZSCORE', KEYS[2], instanceId) then
                local instanceKey = ARGV[1] .. ':entity:' .. ARGV[3] .. ':' .. instanceId
                local payload = redis.call('HGET', instanceKey, 'payload')
                local terminal = false
                if payload then
                    local instance = cjson.decode(payload)
                    local status = tonumber(instance.status)
                    terminal = status == 4 or status == 6 or status == 7 or status == 8 or
                        status == 9 or status == 10 or status == 11
                end
                if terminal then
                    local ownerTypesKey = ARGV[1] .. ':owner-types:' .. instanceId
                    local entityTypes = redis.call('SMEMBERS', ownerTypesKey)
                    for _, entityType in ipairs(entityTypes) do
                        local ownerKey = ARGV[1] .. ':owner:' .. instanceId .. ':' .. entityType
                        local entityIds = redis.call('SMEMBERS', ownerKey)
                        for _, entityId in ipairs(entityIds) do
                            redis.call('UNLINK', ARGV[1] .. ':entity:' .. entityType .. ':' .. entityId)
                        end
                        redis.call('UNLINK', ownerKey)
                    end
                    local ownerLookupsKey = ARGV[1] .. ':owner-lookups:' .. instanceId
                    local lookupKeys = redis.call('SMEMBERS', ownerLookupsKey)
                    for _, lookupKey in ipairs(lookupKeys) do
                        redis.call('UNLINK', lookupKey)
                    end
                    redis.call('UNLINK', ownerLookupsKey)
                    redis.call('UNLINK', ownerTypesKey)
                    removed = removed + 1
                end
                redis.call('ZREM', KEYS[1], instanceId)
            end
        end
        return removed
        """;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly string InstanceEntityType = typeof(OrchestrationInstance).FullName!;
    private readonly RuntimeHotStateRedisOptions _options;
    private readonly IServiceProvider _services;
    private readonly SemaphoreSlim _connectionGate = new(1, 1);
    private IConnectionMultiplexer? _connection;
    private bool _ownsConnection;

    public RedisRuntimeHotStateStore(RuntimeHotStateRedisOptions options, IServiceProvider services)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _services = services ?? throw new ArgumentNullException(nameof(services));
    }

    public bool IsEnabled => true;

    public async Task<long> CommitAsync(IReadOnlyCollection<RuntimeHotStateMutation> mutations, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mutations);
        if (mutations.Count == 0) return await ReadWatermark(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        var database = await GetDatabase(cancellationToken);
        var payload = JsonSerializer.Serialize(mutations, JsonOptions);
        var result = await database.ScriptEvaluateAsync(CommitScript,
            [SequenceKey, JournalKey, PendingKey, PendingInstancesKey],
            [payload, Root, InstanceEntityType]);
        cancellationToken.ThrowIfCancellationRequested();
        return (long)result;
    }

    public async Task CacheAsync(RuntimeHotStateMutation mutation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mutation);
        cancellationToken.ThrowIfCancellationRequested();
        var database = await GetDatabase(cancellationToken);
        await database.ScriptEvaluateAsync(CacheScript, [WatermarkKey, ProjectedTerminalKey],
            [JsonSerializer.Serialize(mutation, JsonOptions), Root, InstanceEntityType]);
        cancellationToken.ThrowIfCancellationRequested();
    }

    public async Task<RuntimeHotStateRecord?> TryReadAsync(string entityType, string entityId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(entityType) || string.IsNullOrWhiteSpace(entityId)) return null;
        cancellationToken.ThrowIfCancellationRequested();
        var database = await GetDatabase(cancellationToken);
        var values = await database.HashGetAsync(EntityKey(entityType, entityId),
            ["entityType", "entityId", "instanceId", "operation", "payload", "sequence"]);
        cancellationToken.ThrowIfCancellationRequested();
        return Materialize(values);
    }

    public async Task<IReadOnlyCollection<RuntimeHotStateRecord>> ReadByInstanceAsync(string entityType, Id instanceId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var database = await GetDatabase(cancellationToken);
        var ids = await database.SetMembersAsync(OwnerKey(instanceId, entityType));
        if (ids.Length == 0) return [];
        var tasks = ids.Select(id => TryReadAsync(entityType, id.ToString(), cancellationToken)).ToArray();
        var records = await Task.WhenAll(tasks);
        return records.Where(record => record is not null).Cast<RuntimeHotStateRecord>().ToArray();
    }

    public async Task<RuntimeHotStateRecord?> TryReadByIndexAsync(string entityType, string indexName, string indexValue, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(indexValue)) return null;
        cancellationToken.ThrowIfCancellationRequested();
        var database = await GetDatabase(cancellationToken);
        var id = await database.StringGetAsync(LookupKey(entityType, indexName, indexValue));
        return id.IsNullOrEmpty ? null : await TryReadAsync(entityType, id.ToString(), cancellationToken);
    }

    public async Task<IReadOnlyCollection<RuntimeHotStateRecord>> ReadPendingInstancesAsync(int take, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var database = await GetDatabase(cancellationToken);
        var ids = await database.SortedSetRangeByRankAsync(PendingInstancesKey, 0, Math.Max(0, take - 1), Order.Descending);
        if (ids.Length == 0) return [];
        var records = await Task.WhenAll(ids.Select(id => TryReadAsync(InstanceEntityType, id.ToString(), cancellationToken)));
        return records.Where(record => record is not null).Cast<RuntimeHotStateRecord>().ToArray();
    }

    public async Task<bool> TryAcquireLeaseAsync(Id instanceId, string leaseId, TimeSpan duration, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var database = await GetDatabase(cancellationToken);
        var result = await database.ScriptEvaluateAsync(AcquireLeaseScript, [LeaseKey(instanceId)],
            [leaseId, Math.Max(1L, (long)duration.TotalMilliseconds)]);
        cancellationToken.ThrowIfCancellationRequested();
        return (int)result == 1;
    }

    public async Task ReleaseLeaseAsync(Id instanceId, string leaseId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var database = await GetDatabase(cancellationToken);
        await database.ScriptEvaluateAsync(ReleaseLeaseScript, [LeaseKey(instanceId)], [leaseId]);
    }

    public async Task<RuntimeHotProjectionLease?> TryAcquireProjectionLeaseAsync(int take, TimeSpan leaseDuration, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var database = await GetDatabase(cancellationToken);
        var token = Guid.NewGuid().ToString("N");
        if (!await database.StringSetAsync(ProjectionLockKey, token, leaseDuration, When.NotExists)) return null;
        try
        {
            var sequences = await database.SortedSetRangeByRankAsync(PendingKey, 0, Math.Max(0, take - 1), Order.Ascending);
            if (sequences.Length == 0)
            {
                await ReleaseProjectionLock(database, token);
                return null;
            }
            var payloads = await database.HashGetAsync(JournalKey, sequences);
            var batches = new List<RuntimeHotProjectionBatch>(sequences.Length);
            for (var index = 0; index < sequences.Length; index++)
            {
                if (payloads[index].IsNullOrEmpty) continue;
                var mutations = JsonSerializer.Deserialize<RuntimeHotStateMutation[]>(payloads[index].ToString(), JsonOptions)
                    ?? [];
                batches.Add(new RuntimeHotProjectionBatch(long.Parse(sequences[index].ToString()), mutations));
            }
            if (batches.Count == 0)
            {
                await ReleaseProjectionLock(database, token);
                return null;
            }
            return new RuntimeHotProjectionLease(token, batches);
        }
        catch
        {
            await ReleaseProjectionLock(database, token);
            throw;
        }
    }

    public async Task AcknowledgeProjectionAsync(RuntimeHotProjectionLease lease, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(lease);
        cancellationToken.ThrowIfCancellationRequested();
        var database = await GetDatabase(cancellationToken);
        var terminalInstanceIds = FindTerminalInstanceIds(lease);
        var values = new RedisValue[lease.Batches.Count + terminalInstanceIds.Length + 3];
        values[0] = lease.Token;
        values[1] = lease.Batches.Count;
        var index = 2;
        foreach (var batch in lease.Batches) values[index++] = batch.Sequence;
        values[index++] = terminalInstanceIds.Length;
        foreach (var instanceId in terminalInstanceIds) values[index++] = instanceId;
        await database.ScriptEvaluateAsync(AcknowledgeScript,
            [ProjectionLockKey, PendingKey, JournalKey, WatermarkKey, PendingInstancesKey, ProjectedTerminalKey], values);
    }

    public async Task ReleaseProjectionLeaseAsync(RuntimeHotProjectionLease lease, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(lease);
        var database = await GetDatabase(cancellationToken);
        await ReleaseProjectionLock(database, lease.Token);
    }

    public async Task<int> CleanupProjectedTerminalStateAsync(int take, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var database = await GetDatabase(cancellationToken);
        var result = await database.ScriptEvaluateAsync(CleanupProjectedTerminalScript,
            [ProjectedTerminalKey, PendingInstancesKey],
            [Root, Math.Clamp(take, 1, 1_000), InstanceEntityType]);
        cancellationToken.ThrowIfCancellationRequested();
        return (int)result;
    }

    private static string[] FindTerminalInstanceIds(RuntimeHotProjectionLease lease)
    {
        var latest = new Dictionary<string, int?>(StringComparer.Ordinal);
        foreach (var batch in lease.Batches.OrderBy(batch => batch.Sequence))
        {
            foreach (var mutation in batch.Mutations.Where(mutation => mutation.EntityType == InstanceEntityType))
            {
                if (mutation.Operation == RuntimeHotStateOperation.Delete)
                {
                    latest[mutation.EntityId] = null;
                    continue;
                }
                using var document = JsonDocument.Parse(mutation.Payload);
                latest[mutation.EntityId] = document.RootElement.TryGetProperty("status", out var status)
                    ? status.GetInt32()
                    : null;
            }
        }
        return latest.Where(item => item.Value is 4 or 6 or 7 or 8 or 9 or 10 or 11)
            .Select(item => item.Key).ToArray();
    }

    private async Task<long> ReadWatermark(CancellationToken cancellationToken)
    {
        var database = await GetDatabase(cancellationToken);
        var value = await database.StringGetAsync(WatermarkKey);
        return value.IsNullOrEmpty ? 0 : (long)value;
    }

    private async Task<IDatabase> GetDatabase(CancellationToken cancellationToken)
    {
        if (_connection is not null) return _connection.GetDatabase(_options.Database);
        await _connectionGate.WaitAsync(cancellationToken);
        try
        {
            if (_connection is null)
            {
                if (_options.ConnectionFactory is not null)
                {
                    _connection = await _options.ConnectionFactory(_services, cancellationToken);
                    _ownsConnection = _options.DisposeConnection;
                }
                else
                {
                    _connection = await ConnectionMultiplexer.ConnectAsync(_options.ConnectionString!);
                    _ownsConnection = true;
                }
            }
            return _connection.GetDatabase(_options.Database);
        }
        finally
        {
            _connectionGate.Release();
        }
    }

    private RuntimeHotStateRecord? Materialize(RedisValue[] values)
    {
        if (values.Length != 6 || values[0].IsNullOrEmpty || values[4].IsNull) return null;
        return new RuntimeHotStateRecord(
            values[0].ToString(),
            values[1].ToString(),
            values[2].ToString() is { Length: > 0 } owner ? owner : null,
            (RuntimeHotStateOperation)(int)values[3],
            values[4].ToString(),
            (long)values[5]);
    }

    private async Task ReleaseProjectionLock(IDatabase database, string token)
        => await database.ScriptEvaluateAsync(ReleaseLeaseScript, [ProjectionLockKey], [token]);

    // A hash tag keeps every key touched by the atomic journal scripts in the
    // same Redis Cluster slot while retaining the configured prefix in tooling.
    private string Root
    {
        get
        {
            var prefix = _options.KeyPrefix.Trim().TrimEnd(':');
            return prefix.Contains('{') ? prefix : $"{{{prefix}}}";
        }
    }
    private RedisKey SequenceKey => $"{Root}:sequence";
    private RedisKey WatermarkKey => $"{Root}:watermark";
    private RedisKey JournalKey => $"{Root}:journal";
    private RedisKey PendingKey => $"{Root}:pending";
    private RedisKey PendingInstancesKey => $"{Root}:pending-instances";
    private RedisKey ProjectedTerminalKey => $"{Root}:projected-terminal";
    private RedisKey ProjectionLockKey => $"{Root}:projection-lock";
    private RedisKey EntityKey(string type, string id) => $"{Root}:entity:{type}:{id}";
    private RedisKey OwnerKey(Id instanceId, string type) => $"{Root}:owner:{instanceId}:{type}";
    private RedisKey LeaseKey(Id instanceId) => $"{Root}:lease:{instanceId}";
    private RedisKey LookupKey(string type, string name, string value)
    {
        var digest = Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes($"{type}|{name}|{value}"))).ToLowerInvariant();
        return $"{Root}:lookup:{digest}";
    }

    public async ValueTask DisposeAsync()
    {
        if (_ownsConnection && _connection is not null) await _connection.DisposeAsync();
        _connectionGate.Dispose();
    }
}
