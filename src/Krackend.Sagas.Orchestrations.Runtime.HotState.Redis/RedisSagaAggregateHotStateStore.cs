#nullable enable

namespace Krackend.Sagas.Orchestrations.Runtime.HotState.Redis;

using System.Collections.Concurrent;
using System.IO.Compression;
using System.Text.Json;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using StackExchange.Redis;

/// <summary>
/// Redis hot state represented as one versioned JSON tracking aggregate per saga.
/// The foreground path performs one aggregate read and one compare-and-swap patch;
/// durable EF projection is handled by the existing background projector.
/// </summary>
internal sealed class RedisSagaAggregateHotStateStore : IRuntimeHotStateStore,
    IRuntimeHotStateInstanceGraphStore,
    IRuntimeHotStateOptimisticGraphStore,
    IRuntimeHotStateAggregateStore,
    IRuntimeHotStateDispatchReceiptStore,
    IAsyncDisposable
{
    private const int MaximumLocalAggregates = 50_000;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly string InstanceEntityType = typeof(OrchestrationInstance).FullName!;

    private const string CommitScript = """
        local current = tonumber(redis.call('HGET', KEYS[1], 'version') or '0')
        if current ~= tonumber(ARGV[1]) then
            return redis.error_reply('runtime saga aggregate version conflict')
        end
        local next = current + 1
        redis.call('HSET', KEYS[1], 'version', tostring(next), 'json', ARGV[2])
        redis.call('ZADD', KEYS[2], next, ARGV[3])
        return next
        """;

    private const string CacheScript = """
        if redis.call('EXISTS', KEYS[1]) == 1 then
            return tonumber(redis.call('HGET', KEYS[1], 'version') or '0')
        end
        redis.call('HSET', KEYS[1], 'version', '1', 'json', ARGV[1])
        return 1
        """;

    private const string ReleaseScript = """
        if redis.call('GET', KEYS[1]) == ARGV[1] then
            return redis.call('DEL', KEYS[1])
        end
        return 0
        """;

    private const string AcknowledgeScript = """
        if redis.call('GET', KEYS[1]) ~= ARGV[1] then return 0 end
        local count = tonumber(ARGV[3])
        local offset = 4
        local acknowledged = 0
        for index = 1, count do
            local instanceId = ARGV[offset]
            local version = ARGV[offset + 1]
            local terminal = ARGV[offset + 2]
            local aggregateKey = ARGV[2] .. ':saga:' .. instanceId
            local current = redis.call('HGET', aggregateKey, 'version') or '0'
            if current == version then
                redis.call('ZREM', KEYS[2], instanceId)
                if terminal == '1' then
                    redis.call('ZADD', KEYS[3], tonumber(version), instanceId)
                end
                acknowledged = acknowledged + 1
            end
            offset = offset + 3
        end
        redis.call('DEL', KEYS[1])
        return acknowledged
        """;

    private const string CleanupScript = """
        local ids = redis.call('ZRANGE', KEYS[1], 0, tonumber(ARGV[1]) - 1)
        local removed = 0
        for _, instanceId in ipairs(ids) do
            if not redis.call('ZSCORE', KEYS[2], instanceId) then
                redis.call('UNLINK', ARGV[2] .. ':saga:' .. instanceId)
                redis.call('ZREM', KEYS[1], instanceId)
                removed = removed + 1
            end
        end
        return removed
        """;

    private readonly RuntimeHotStateRedisOptions _options;
    private readonly IServiceProvider _services;
    private readonly SemaphoreSlim _connectionGate = new(1, 1);
    private readonly ConcurrentDictionary<Id, CachedAggregate> _aggregates = new();
    private readonly ConcurrentDictionary<string, RuntimeHotStateRecord> _entities = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, string> _indexes = new(StringComparer.Ordinal);
    private long _generation;
    private int _projectionCursor;
    private int _cleanupCursor;
    private IConnectionMultiplexer? _connection;
    private bool _ownsConnection;

    public RedisSagaAggregateHotStateStore(
        RuntimeHotStateRedisOptions options,
        IServiceProvider services)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _services = services ?? throw new ArgumentNullException(nameof(services));
    }

    public bool IsEnabled => true;

    public async Task<RuntimeHotStateLeaseGraph> ReadGraphFenceAsync(
        Id instanceId,
        CancellationToken cancellationToken = default)
    {
        SagaAggregateDocument? aggregate = null;
        if (_aggregates.TryGetValue(instanceId, out var cached))
        {
            var database = await GetDatabase(cancellationToken);
            var value = await database.HashGetAsync(AggregateKey(instanceId), "version");
            cancellationToken.ThrowIfCancellationRequested();
            if (value.TryParse(out long version) && version == cached.Version)
                aggregate = new SagaAggregateDocument(cached.Version, cached.Records.ToArray());
        }
        aggregate ??= await ReadAggregateAsync(instanceId, cancellationToken, force: true);
        return new RuntimeHotStateLeaseGraph(
            aggregate?.Version ?? 0,
            aggregate?.Records ?? [],
            true);
    }

    public async Task<long> CommitFencedAsync(
        IReadOnlyCollection<RuntimeHotStateMutation> mutations,
        Id instanceId,
        long expectedVersion,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mutations);
        if (mutations.Count == 0) return expectedVersion;
        SagaAggregateDocument? current = null;
        if (_aggregates.TryGetValue(instanceId, out var cached) && cached.Version == expectedVersion)
            current = new SagaAggregateDocument(cached.Version, cached.Records.ToArray());
        current ??= await ReadAggregateAsync(instanceId, cancellationToken, force: true);
        if ((current?.Version ?? 0) != expectedVersion)
            throw new RuntimeStateConflictException(
                $"Saga aggregate '{instanceId}' changed before its Redis patch.");

        var nextVersion = expectedVersion + 1;
        var records = Merge(current?.Records ?? [], mutations, instanceId, nextVersion);
        var document = new SagaAggregateDocument(nextVersion, records);
        var database = await GetDatabase(cancellationToken);
        RedisResult result;
        try
        {
            result = await database.ScriptEvaluateAsync(
                CommitScript,
                [AggregateKey(instanceId), PendingKey(Lane(instanceId))],
                [expectedVersion, SerializeDocument(document), instanceId.ToString()]);
        }
        catch (RedisServerException exception) when (
            exception.Message.Contains("aggregate version conflict", StringComparison.Ordinal))
        {
            RemoveLocalAggregate(instanceId);
            throw;
        }
        cancellationToken.ThrowIfCancellationRequested();
        var committedVersion = (long)result;
        StoreLocalAggregate(instanceId, new SagaAggregateDocument(committedVersion, records), mutations);
        return committedVersion;
    }

    public async Task<long> CommitAsync(
        IReadOnlyCollection<RuntimeHotStateMutation> mutations,
        CancellationToken cancellationToken = default)
    {
        var instanceId = ResolveInstanceId(mutations);
        var current = await ReadGraphFenceAsync(instanceId, cancellationToken);
        return await CommitFencedAsync(mutations, instanceId, current.Version, cancellationToken);
    }

    public async Task<long> CommitAndReleaseAsync(
        IReadOnlyCollection<RuntimeHotStateMutation> mutations,
        Id instanceId,
        string leaseId,
        CancellationToken cancellationToken = default)
    {
        var current = await ReadGraphFenceAsync(instanceId, cancellationToken);
        var version = await CommitFencedAsync(mutations, instanceId, current.Version, cancellationToken);
        await ReleaseLeaseAsync(instanceId, leaseId, cancellationToken);
        return version;
    }

    public async Task CacheAggregateAsync(
        Id instanceId,
        IReadOnlyCollection<RuntimeHotStateMutation> mutations,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mutations);
        if (mutations.Count == 0) return;
        var records = Merge([], mutations, instanceId, 1);
        var document = new SagaAggregateDocument(1, records);
        var database = await GetDatabase(cancellationToken);
        var version = (long)await database.ScriptEvaluateAsync(
            CacheScript,
            [AggregateKey(instanceId)],
            [SerializeDocument(document)]);
        if (version == 1)
        {
            StoreLocalAggregate(instanceId, document, mutations);
            return;
        }
        await ReadAggregateAsync(instanceId, cancellationToken, force: true);
    }

    public Task CacheAsync(
        RuntimeHotStateMutation mutation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mutation);
        var record = ToRecord(mutation, 0, mutation.InstanceId);
        _entities[EntityCacheKey(record.EntityType, record.EntityId)] = record;
        CacheIndexes([mutation]);
        return Task.CompletedTask;
    }

    public Task<RuntimeHotStateRecord?> TryReadAsync(
        string entityType,
        string entityId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _entities.TryGetValue(EntityCacheKey(entityType, entityId), out var record);
        return Task.FromResult(record);
    }

    public async Task<IReadOnlyCollection<RuntimeHotStateRecord>> ReadByInstanceAsync(
        string entityType,
        Id instanceId,
        CancellationToken cancellationToken = default)
    {
        var aggregate = await ReadAggregateAsync(instanceId, cancellationToken);
        return aggregate?.Records
            .Where(record => string.Equals(record.EntityType, entityType, StringComparison.Ordinal))
            .ToArray() ?? [];
    }

    public async Task<IReadOnlyCollection<RuntimeHotStateRecord>> ReadInstanceGraphAsync(
        Id instanceId,
        CancellationToken cancellationToken = default)
        => (await ReadAggregateAsync(instanceId, cancellationToken))?.Records ?? [];

    public Task<RuntimeHotStateRecord?> TryReadByIndexAsync(
        string entityType,
        string indexName,
        string indexValue,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_indexes.TryGetValue(IndexCacheKey(entityType, indexName, indexValue), out var entityId) &&
            _entities.TryGetValue(EntityCacheKey(entityType, entityId), out var record))
            return Task.FromResult<RuntimeHotStateRecord?>(record);
        return Task.FromResult<RuntimeHotStateRecord?>(null);
    }

    public async Task<IReadOnlyCollection<RuntimeHotStateRecord>> ReadPendingInstancesAsync(
        int take,
        CancellationToken cancellationToken = default)
    {
        var database = await GetDatabase(cancellationToken);
        var reads = Enumerable.Range(0, LaneCount).Select(async lane =>
        {
            var ids = await database.SortedSetRangeByRankAsync(
                PendingKey(lane), 0, Math.Max(0, take - 1), Order.Descending);
            var documents = await Task.WhenAll(ids.Select(async value =>
                await ReadAggregateAsync(ParseId(value), cancellationToken, force: true)));
            return documents.Where(document => document is not null)
                .SelectMany(document => document!.Records)
                .Where(record => record.EntityType == InstanceEntityType);
        });
        return (await Task.WhenAll(reads)).SelectMany(records => records).Take(take).ToArray();
    }

    public async Task<bool> TryAcquireLeaseAsync(
        Id instanceId,
        string leaseId,
        TimeSpan duration,
        CancellationToken cancellationToken = default)
    {
        var database = await GetDatabase(cancellationToken);
        return await database.StringSetAsync(
            LeaseKey(instanceId), leaseId, duration, When.NotExists);
    }

    public async Task ReleaseLeaseAsync(
        Id instanceId,
        string leaseId,
        CancellationToken cancellationToken = default)
    {
        var database = await GetDatabase(cancellationToken);
        await database.ScriptEvaluateAsync(ReleaseScript, [LeaseKey(instanceId)], [leaseId]);
    }

    public async Task<RuntimeHotProjectionLease?> TryAcquireProjectionLeaseAsync(
        int take,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default)
    {
        var database = await GetDatabase(cancellationToken);
        var size = Math.Min(Math.Max(1, take), Math.Max(1, _options.MaxProjectionLeaseSize));
        for (var offset = 0; offset < LaneCount; offset++)
        {
            var lane = (Interlocked.Increment(ref _projectionCursor) + offset) % LaneCount;
            var token = Guid.NewGuid().ToString("N");
            if (!await database.StringSetAsync(ProjectionLockKey(lane), token, leaseDuration, When.NotExists))
                continue;
            try
            {
                var ids = await database.SortedSetRangeByRankAsync(PendingKey(lane), 0, size - 1);
                if (ids.Length == 0)
                {
                    await ReleaseProjectionLock(database, lane, token);
                    continue;
                }
                var documents = await Task.WhenAll(ids.Select(async value =>
                    (Id: value.ToString(), Document: await ReadAggregateAsync(
                        ParseId(value), cancellationToken, force: true))));
                var batches = documents
                    .Where(item => item.Document is not null)
                    .Select(item => new RuntimeHotProjectionBatch(
                        item.Document!.Version,
                        item.Document.Records.Select(record => new RuntimeHotStateMutation(
                            record.EntityType,
                            record.EntityId,
                            record.InstanceId,
                            record.Operation,
                            record.Payload)).ToArray(),
                        item.Id))
                    .ToArray();
                if (batches.Length == 0)
                {
                    await ReleaseProjectionLock(database, lane, token);
                    continue;
                }
                return new RuntimeHotProjectionLease($"{lane}|{token}", batches);
            }
            catch
            {
                await ReleaseProjectionLock(database, lane, token);
                throw;
            }
        }
        return null;
    }

    public async Task AcknowledgeProjectionAsync(
        RuntimeHotProjectionLease lease,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(lease);
        var (lane, token) = ParseProjectionToken(lease.Token);
        var terminals = FindTerminalInstanceIds(lease).ToHashSet(StringComparer.Ordinal);
        var values = new RedisValue[3 + (lease.Batches.Count * 3)];
        values[0] = token;
        values[1] = LaneRoot(lane);
        values[2] = lease.Batches.Count;
        var index = 3;
        foreach (var batch in lease.Batches)
        {
            values[index++] = batch.InstanceId!;
            values[index++] = batch.Sequence;
            values[index++] = terminals.Contains(batch.InstanceId!) ? 1 : 0;
        }
        var database = await GetDatabase(cancellationToken);
        await database.ScriptEvaluateAsync(
            AcknowledgeScript,
            [ProjectionLockKey(lane), PendingKey(lane), TerminalKey(lane)],
            values);
    }

    public async Task ReleaseProjectionLeaseAsync(
        RuntimeHotProjectionLease lease,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(lease);
        var (lane, token) = ParseProjectionToken(lease.Token);
        var database = await GetDatabase(cancellationToken);
        await ReleaseProjectionLock(database, lane, token);
    }

    public async Task<int> CleanupProjectedTerminalStateAsync(
        int take,
        CancellationToken cancellationToken = default)
    {
        var lane = Math.Abs(Interlocked.Increment(ref _cleanupCursor) % LaneCount);
        var database = await GetDatabase(cancellationToken);
        return (int)await database.ScriptEvaluateAsync(
            CleanupScript,
            [TerminalKey(lane), PendingKey(lane)],
            [Math.Min(Math.Max(1, take), _options.TerminalCleanupBatchSize), LaneRoot(lane)]);
    }

    public async Task RecordDispatchPublicationAsync(
        Id dispatchId,
        DateTime publishedOnUtc,
        CancellationToken cancellationToken = default)
    {
        var database = await GetDatabase(cancellationToken);
        await database.StringSetAsync(
            DispatchReceiptKey(dispatchId),
            publishedOnUtc.ToUniversalTime().Ticks,
            TimeSpan.FromDays(7),
            When.NotExists);
    }

    public async Task<DateTime?> TryReadDispatchPublicationAsync(
        Id dispatchId,
        CancellationToken cancellationToken = default)
    {
        var database = await GetDatabase(cancellationToken);
        var value = await database.StringGetAsync(DispatchReceiptKey(dispatchId));
        return value.TryParse(out long ticks) ? new DateTime(ticks, DateTimeKind.Utc) : null;
    }

    private async Task<SagaAggregateDocument?> ReadAggregateAsync(
        Id instanceId,
        CancellationToken cancellationToken,
        bool force = false)
    {
        if (!force && _aggregates.TryGetValue(instanceId, out var cached))
            return new SagaAggregateDocument(cached.Version, cached.Records.ToArray());
        var database = await GetDatabase(cancellationToken);
        var values = await database.HashGetAsync(AggregateKey(instanceId), ["version", "json"]);
        cancellationToken.ThrowIfCancellationRequested();
        if (values.Length != 2 || values[1].IsNullOrEmpty)
        {
            RemoveLocalAggregate(instanceId);
            return null;
        }
        var document = DeserializeDocument(values[1]);
        if (document is null) return null;
        var version = values[0].TryParse(out long parsed) ? parsed : document.Version;
        document = document with { Version = version };
        StoreLocalAggregate(instanceId, document, null);
        return document;
    }

    private void StoreLocalAggregate(
        Id instanceId,
        SagaAggregateDocument document,
        IReadOnlyCollection<RuntimeHotStateMutation>? mutations)
    {
        _aggregates[instanceId] = new CachedAggregate(
            document.Version,
            document.Records,
            Interlocked.Increment(ref _generation));
        foreach (var record in document.Records)
            _entities[EntityCacheKey(record.EntityType, record.EntityId)] = record;
        if (mutations is not null) CacheIndexes(mutations);
        if (_aggregates.Count <= MaximumLocalAggregates) return;
        foreach (var candidate in _aggregates.OrderBy(item => item.Value.Generation)
            .Take(Math.Max(1, _aggregates.Count - MaximumLocalAggregates)))
            RemoveLocalAggregate(candidate.Key);
    }

    private RedisValue SerializeDocument(SagaAggregateDocument document)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(document, JsonOptions);
        if (_options.PayloadCompressionThresholdBytes > 0 &&
            json.Length >= _options.PayloadCompressionThresholdBytes)
        {
            var compressed = new byte[BrotliEncoder.GetMaxCompressedLength(json.Length) + 1];
            compressed[0] = 1;
            if (BrotliEncoder.TryCompress(
                    json,
                    compressed.AsSpan(1),
                    out var written,
                    quality: 1,
                    window: 20) &&
                written + 1 < json.Length)
            {
                Array.Resize(ref compressed, written + 1);
                return compressed;
            }
        }

        var plain = new byte[json.Length + 1];
        plain[0] = 0;
        json.CopyTo(plain.AsSpan(1));
        return plain;
    }

    private static SagaAggregateDocument? DeserializeDocument(RedisValue value)
    {
        var stored = (byte[]?)value;
        if (stored is null || stored.Length == 0) return null;
        if (stored[0] == 1)
        {
            using var input = new MemoryStream(stored, 1, stored.Length - 1, writable: false);
            using var brotli = new BrotliStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            brotli.CopyTo(output);
            return JsonSerializer.Deserialize<SagaAggregateDocument>(
                output.GetBuffer().AsSpan(0, checked((int)output.Length)), JsonOptions);
        }

        var json = stored[0] == 0 ? stored.AsSpan(1) : stored.AsSpan();
        return JsonSerializer.Deserialize<SagaAggregateDocument>(json, JsonOptions);
    }

    private void RemoveLocalAggregate(Id instanceId)
    {
        if (!_aggregates.TryRemove(instanceId, out var removed)) return;
        foreach (var record in removed.Records)
            _entities.TryRemove(EntityCacheKey(record.EntityType, record.EntityId), out _);
    }

    private void CacheIndexes(IEnumerable<RuntimeHotStateMutation> mutations)
    {
        foreach (var mutation in mutations)
        foreach (var pair in mutation.Indexes ?? new Dictionary<string, string>())
            _indexes[IndexCacheKey(mutation.EntityType, pair.Key, pair.Value)] = mutation.EntityId;
    }

    private static RuntimeHotStateRecord[] Merge(
        IReadOnlyCollection<RuntimeHotStateRecord> current,
        IReadOnlyCollection<RuntimeHotStateMutation> mutations,
        Id instanceId,
        long version)
    {
        var records = current.ToDictionary(
            record => EntityCacheKey(record.EntityType, record.EntityId),
            StringComparer.Ordinal);
        foreach (var mutation in mutations)
        {
            var owner = string.IsNullOrWhiteSpace(mutation.InstanceId)
                ? instanceId.ToString()
                : mutation.InstanceId;
            records[EntityCacheKey(mutation.EntityType, mutation.EntityId)] =
                ToRecord(mutation, version, owner);
        }
        return records.Values.ToArray();
    }

    private static RuntimeHotStateRecord ToRecord(
        RuntimeHotStateMutation mutation,
        long version,
        string? instanceId)
        => new(
            mutation.EntityType,
            mutation.EntityId,
            instanceId,
            mutation.Operation,
            mutation.Payload,
            version);

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
                    var supplied = await _options.ConnectionFactory(_services, cancellationToken);
                    if (_options.UseDedicatedConnection && !_options.DisposeConnection)
                    {
                        _connection = await ConnectionMultiplexer.ConnectAsync(supplied.Configuration);
                        _ownsConnection = true;
                    }
                    else
                    {
                        _connection = supplied;
                        _ownsConnection = _options.DisposeConnection;
                    }
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

    private async Task ReleaseProjectionLock(IDatabase database, int lane, string token)
        => await database.ScriptEvaluateAsync(ReleaseScript, [ProjectionLockKey(lane)], [token]);

    private int LaneCount => Math.Clamp(_options.AggregateLaneCount, 1, 256);
    private int Lane(Id id)
    {
        unchecked
        {
            uint hash = 2166136261;
            foreach (var character in id.ToString()) hash = (hash ^ character) * 16777619;
            return (int)(hash % (uint)LaneCount);
        }
    }

    private string BasePrefix => _options.KeyPrefix.Trim().TrimEnd(':').Replace("{", "").Replace("}", "");
    private string LaneRoot(int lane) => $"{{{BasePrefix}:aggregate:{lane}}}";
    private RedisKey AggregateKey(Id id) => $"{LaneRoot(Lane(id))}:saga:{id}";
    private RedisKey PendingKey(int lane) => $"{LaneRoot(lane)}:pending";
    private RedisKey TerminalKey(int lane) => $"{LaneRoot(lane)}:terminal";
    private RedisKey ProjectionLockKey(int lane) => $"{LaneRoot(lane)}:projection-lock";
    private RedisKey LeaseKey(Id id) => $"{LaneRoot(Lane(id))}:lease:{id}";
    private RedisKey DispatchReceiptKey(Id id) => $"{LaneRoot(Lane(id))}:dispatch:{id}";

    private static string EntityCacheKey(string type, string id) => $"{type}|{id}";
    private static string IndexCacheKey(string type, string name, string value) => $"{type}|{name}|{value}";
    private static Id ParseId(RedisValue value) => new(Ulid.Parse(value.ToString()));

    private static Id ResolveInstanceId(IReadOnlyCollection<RuntimeHotStateMutation> mutations)
    {
        var value = mutations.Select(mutation => mutation.InstanceId)
            .FirstOrDefault(candidate => !string.IsNullOrWhiteSpace(candidate));
        return value is null
            ? throw new InvalidOperationException("A saga aggregate mutation requires an instance id.")
            : new Id(Ulid.Parse(value));
    }

    private static (int Lane, string Token) ParseProjectionToken(string value)
    {
        var parts = value.Split('|', 2);
        if (parts.Length != 2 || !int.TryParse(parts[0], out var lane))
            throw new InvalidOperationException("Invalid Redis saga projection token.");
        return (lane, parts[1]);
    }

    private static string[] FindTerminalInstanceIds(RuntimeHotProjectionLease lease)
    {
        var terminal = new List<string>();
        foreach (var batch in lease.Batches)
        {
            var instance = batch.Mutations.LastOrDefault(mutation => mutation.EntityType == InstanceEntityType);
            if (instance is null || instance.Operation == RuntimeHotStateOperation.Delete) continue;
            using var document = JsonDocument.Parse(instance.Payload);
            if (document.RootElement.TryGetProperty("status", out var status) &&
                status.GetInt32() is 4 or 6 or 7 or 8 or 9 or 10 or 11 &&
                !string.IsNullOrWhiteSpace(batch.InstanceId))
                terminal.Add(batch.InstanceId!);
        }
        return terminal.ToArray();
    }

    public async ValueTask DisposeAsync()
    {
        if (_ownsConnection && _connection is not null) await _connection.DisposeAsync();
        _connectionGate.Dispose();
    }

    private sealed record SagaAggregateDocument(long Version, RuntimeHotStateRecord[] Records);
    private sealed record CachedAggregate(
        long Version,
        IReadOnlyCollection<RuntimeHotStateRecord> Records,
        long Generation);
}
