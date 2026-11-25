#nullable enable

namespace Krackend.Sagas.Orchestrations.Runtime.HotState.Redis;

using System.Collections.Concurrent;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using StackExchange.Redis;

internal sealed class RedisRuntimeHotStateStore : IRuntimeHotStateStore,
    IRuntimeHotStateInstanceGraphStore, IRuntimeHotStateLeaseGraphStore,
    IRuntimeHotStateOptimisticGraphStore,
    IRuntimeHotStateDispatchReceiptStore, IAsyncDisposable
{
    private const char RecordSeparator = (char)30;
    private const string CompressedPayloadPrefix = "~br~";
    private const string GraphVersionField = "#version";
    private const int MaximumLocalGraphs = 50_000;
    private const string CommitScript = """
        local leaseId = ARGV[5]
        local expectedVersion = ARGV[6]
        if expectedVersion ~= '' then
            local currentVersion = redis.call('HGET', KEYS[7], '#version') or '0'
            if currentVersion ~= expectedVersion then
                return redis.error_reply('runtime hot-state graph version conflict')
            end
        elseif leaseId ~= '' and redis.call('GET', KEYS[5]) ~= leaseId then
            return redis.error_reply('runtime hot-state mutation lease was lost')
        end
        local mutationCount = tonumber(ARGV[4])
        local offset = 7
        for mutationIndex = 1, mutationCount do
            local entityType = ARGV[offset]
            local entityId = ARGV[offset + 1]
            local indexCount = tonumber(ARGV[offset + 5])
            offset = offset + 6
            for index = 1, indexCount do
                local name = ARGV[offset]
                local value = ARGV[offset + 1]
                local digest = ARGV[offset + 2]
                if entityType == ARGV[3] and name == 'start' and value ~= '' then
                    local owner = redis.call('HGET', KEYS[6], digest)
                    if not owner then
                        owner = redis.call('GET', ARGV[2] .. ':lookup:' .. digest)
                    end
                    if owner and owner ~= entityId then
                        return redis.error_reply('duplicate runtime start idempotency key')
                    end
                end
                offset = offset + 3
            end
        end
        local sequence = redis.call('INCR', KEYS[1])
        redis.call('HSET', KEYS[2], tostring(sequence), ARGV[1])
        redis.call('ZADD', KEYS[3], sequence, tostring(sequence))
        local separator = string.char(30)
        local graphValues = {}
        local lookupValues = {}
        offset = 7
        for mutationIndex = 1, mutationCount do
            local entityType = ARGV[offset]
            local entityId = ARGV[offset + 1]
            local instanceId = ARGV[offset + 2]
            local operation = ARGV[offset + 3]
            local payload = ARGV[offset + 4]
            local indexCount = tonumber(ARGV[offset + 5])
            offset = offset + 6
            local entityKey = ARGV[2] .. ':entity:' .. entityType .. ':' .. entityId
            local record =
                entityType .. separator .. entityId .. separator .. instanceId .. separator ..
                operation .. separator .. tostring(sequence) .. separator .. payload
            redis.call('SET', entityKey, record)
            if instanceId ~= '' then
                table.insert(graphValues, entityType .. separator .. entityId)
                table.insert(graphValues, record)
            end
            for index = 1, indexCount do
                local digest = ARGV[offset + 2]
                table.insert(lookupValues, digest)
                table.insert(lookupValues, entityId)
                if instanceId ~= '' then
                    table.insert(graphValues, '@' .. digest)
                    table.insert(graphValues, '')
                end
                offset = offset + 3
            end
            if entityType == ARGV[3] then
                redis.call('ZADD', KEYS[4], sequence, entityId)
            end
        end
        if #lookupValues > 0 then
            redis.call('HSET', KEYS[6], unpack(lookupValues))
        end
        if #graphValues > 0 then
            table.insert(graphValues, '#version')
            table.insert(graphValues, tostring(sequence))
            redis.call('HSET', KEYS[7], unpack(graphValues))
        end
        if leaseId ~= '' then
            redis.call('DEL', KEYS[5])
        end
        return sequence
        """;

    private const string CacheScript = """
        local mutation = cjson.decode(ARGV[1])
        local entityKey = ARGV[2] .. ':entity:' .. mutation.entityType .. ':' .. mutation.entityId
        if redis.call('EXISTS', entityKey) == 1 then return 0 end
        local sequence = tonumber(redis.call('GET', KEYS[1]) or '0')
        local instanceId = type(mutation.instanceId) == 'string' and mutation.instanceId or ''
        local separator = string.char(30)
        local record =
            mutation.entityType .. separator .. mutation.entityId .. separator .. instanceId .. separator ..
            tostring(mutation.operation) .. separator .. tostring(sequence) .. separator .. mutation.payload
        redis.call('SET', entityKey, record)
        if type(mutation.instanceId) == 'string' and mutation.instanceId ~= '' then
            redis.call('HSET', ARGV[2] .. ':graph:' .. mutation.instanceId,
                mutation.entityType .. separator .. mutation.entityId, record,
                '#version', tostring(sequence))
        end
        if type(mutation.indexes) == 'table' then
            for name, value in pairs(mutation.indexes) do
                local digest = redis.sha1hex(mutation.entityType .. '|' .. name .. '|' .. value)
                redis.call('HSET', KEYS[3], digest, mutation.entityId)
                if type(mutation.instanceId) == 'string' and mutation.instanceId ~= '' then
                    redis.call('HSET', ARGV[2] .. ':graph:' .. mutation.instanceId, '@' .. digest, '')
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

    private const string AcquireLeaseGraphScript = """
        local current = redis.call('GET', KEYS[1])
        if current and current ~= ARGV[1] then return {0} end
        redis.call('SET', KEYS[1], ARGV[1], 'PX', ARGV[2])
        local version = redis.call('HGET', KEYS[2], '#version') or '0'
        if version == ARGV[3] then return {1, version} end
        local graph = redis.call('HVALS', KEYS[2])
        local result = {1, version}
        for _, value in ipairs(graph) do table.insert(result, value) end
        return result
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
                local payload = false
                local keyType = redis.call('TYPE', instanceKey).ok
                if keyType == 'string' then
                    local record = redis.call('GET', instanceKey)
                    local separator = string.char(30)
                    local start = 1
                    for part = 1, 5 do
                        local position = string.find(record, separator, start, true)
                        if not position then
                            start = nil
                            break
                        end
                        start = position + 1
                    end
                    if start then payload = string.sub(record, start) end
                elseif keyType == 'hash' then
                    payload = redis.call('HGET', instanceKey, 'payload')
                end
                local terminal = false
                if payload then
                    local instance = cjson.decode(payload)
                    local status = tonumber(instance.status)
                    terminal = status == 4 or status == 6 or status == 7 or status == 8 or
                        status == 9 or status == 10 or status == 11
                end
                if terminal then
                    local graphKey = ARGV[1] .. ':graph:' .. instanceId
                    if redis.call('TYPE', graphKey).ok == 'hash' then
                        local separator = string.char(30)
                        local graphFields = redis.call('HKEYS', graphKey)
                        for _, field in ipairs(graphFields) do
                            if string.sub(field, 1, 1) == '@' then
                                redis.call('HDEL', KEYS[3], string.sub(field, 2))
                            else
                                local position = string.find(field, separator, 1, true)
                                if position then
                                local entityType = string.sub(field, 1, position - 1)
                                local entityId = string.sub(field, position + 1)
                                redis.call('UNLINK', ARGV[1] .. ':entity:' .. entityType .. ':' .. entityId)
                                end
                            end
                        end
                        redis.call('UNLINK', graphKey)
                    else
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
                        redis.call('UNLINK', ownerTypesKey)
                    end
                    local ownerLookupsKey = ARGV[1] .. ':owner-lookups:' .. instanceId
                    local lookupKeys = redis.call('SMEMBERS', ownerLookupsKey)
                    for _, lookupKey in ipairs(lookupKeys) do
                        redis.call('UNLINK', lookupKey)
                    end
                    redis.call('UNLINK', ownerLookupsKey)
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
    private readonly ConcurrentDictionary<Id, LocalGraph> _localGraphs = new();
    private long _localGraphGeneration;
    private IConnectionMultiplexer? _connection;
    private bool _ownsConnection;

    public RedisRuntimeHotStateStore(RuntimeHotStateRedisOptions options, IServiceProvider services)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _services = services ?? throw new ArgumentNullException(nameof(services));
    }

    public bool IsEnabled => true;

    public async Task<long> CommitAsync(IReadOnlyCollection<RuntimeHotStateMutation> mutations, CancellationToken cancellationToken = default)
        => await CommitCoreAsync(mutations, null, null, null, cancellationToken);

    public async Task<long> CommitAndReleaseAsync(
        IReadOnlyCollection<RuntimeHotStateMutation> mutations,
        Id instanceId,
        string leaseId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(leaseId))
            throw new ArgumentException("A non-empty mutation lease id is required.", nameof(leaseId));
        return await CommitCoreAsync(mutations, instanceId, leaseId, null, cancellationToken);
    }

    public async Task<long> CommitFencedAsync(
        IReadOnlyCollection<RuntimeHotStateMutation> mutations,
        Id instanceId,
        long expectedVersion,
        CancellationToken cancellationToken = default)
        => await CommitCoreAsync(mutations, instanceId, null, expectedVersion, cancellationToken);

    private async Task<long> CommitCoreAsync(
        IReadOnlyCollection<RuntimeHotStateMutation> mutations,
        Id? instanceId,
        string? leaseId,
        long? expectedVersion,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mutations);
        if (mutations.Count == 0) return await ReadWatermark(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        var ownerId = instanceId?.ToString() ?? mutations
            .Select(mutation => mutation.InstanceId)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        if (string.IsNullOrWhiteSpace(ownerId))
            throw new InvalidOperationException("A hot-state mutation batch must belong to one orchestration instance.");
        if (mutations.Any(mutation => !string.IsNullOrWhiteSpace(mutation.InstanceId) &&
            !string.Equals(mutation.InstanceId, ownerId, StringComparison.Ordinal)))
            throw new InvalidOperationException("A hot-state mutation batch cannot span orchestration instances.");
        var ownerInstanceId = instanceId ?? new Id(Ulid.Parse(ownerId));
        var effectiveMutations = mutations.Any(mutation => string.IsNullOrWhiteSpace(mutation.InstanceId))
            ? mutations.Select(mutation => string.IsNullOrWhiteSpace(mutation.InstanceId)
                ? mutation with { InstanceId = ownerId }
                : mutation).ToArray()
            : mutations;
        var database = await GetDatabase(cancellationToken);
        var payload = JsonSerializer.Serialize(
            effectiveMutations.Select(mutation => new RuntimeHotJournalReference(
                mutation.EntityType,
                mutation.EntityId)).ToArray(),
            JsonOptions);
        var values = BuildCommitValues(effectiveMutations, payload, leaseId, expectedVersion);
        RedisResult result;
        try
        {
            result = await database.ScriptEvaluateAsync(CommitScript,
                [SequenceKey, JournalKey, PendingKey, PendingInstancesKey,
                    LeaseKey(ownerInstanceId), LookupsKey, GraphKey(ownerInstanceId)],
                values);
        }
        catch (RedisServerException exception) when (
            exception.Message.Contains("graph version conflict", StringComparison.Ordinal))
        {
            _localGraphs.TryRemove(ownerInstanceId, out _);
            throw;
        }
        cancellationToken.ThrowIfCancellationRequested();
        var sequence = (long)result;
        UpdateLocalGraphs(effectiveMutations, sequence);
        return sequence;
    }

    public async Task CacheAsync(RuntimeHotStateMutation mutation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mutation);
        cancellationToken.ThrowIfCancellationRequested();
        var database = await GetDatabase(cancellationToken);
        await database.ScriptEvaluateAsync(CacheScript, [WatermarkKey, ProjectedTerminalKey, LookupsKey],
            [JsonSerializer.Serialize(mutation, JsonOptions), Root, InstanceEntityType]);
        cancellationToken.ThrowIfCancellationRequested();
    }

    public async Task<RuntimeHotStateRecord?> TryReadAsync(string entityType, string entityId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(entityType) || string.IsNullOrWhiteSpace(entityId)) return null;
        cancellationToken.ThrowIfCancellationRequested();
        var database = await GetDatabase(cancellationToken);
        RedisValue value;
        try
        {
            value = await database.StringGetAsync(EntityKey(entityType, entityId));
        }
        catch (RedisServerException exception) when (exception.Message.Contains("WRONGTYPE", StringComparison.Ordinal))
        {
            return await TryReadLegacyHashAsync(database, entityType, entityId, cancellationToken);
        }
        cancellationToken.ThrowIfCancellationRequested();
        return Materialize(value);
    }

    public async Task<IReadOnlyCollection<RuntimeHotStateRecord>> ReadByInstanceAsync(string entityType, Id instanceId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var database = await GetDatabase(cancellationToken);
        var graphValues = await database.HashValuesAsync(GraphKey(instanceId));
        if (graphValues.Length > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return graphValues.Select(Materialize)
                .Where(record => record is not null &&
                    string.Equals(record.EntityType, entityType, StringComparison.Ordinal))
                .Cast<RuntimeHotStateRecord>()
                .ToArray();
        }

        var ids = await database.SetMembersAsync(OwnerKey(instanceId, entityType));
        if (ids.Length == 0) return [];
        return await ReadManyAsync(database, entityType, ids, cancellationToken);
    }

    public async Task<IReadOnlyCollection<RuntimeHotStateRecord>> ReadInstanceGraphAsync(
        Id instanceId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var database = await GetDatabase(cancellationToken);
        var graphValues = await database.HashValuesAsync(GraphKey(instanceId));
        cancellationToken.ThrowIfCancellationRequested();
        if (graphValues.Length > 0)
        {
            return graphValues.Select(Materialize)
                .Where(record => record is not null)
                .Cast<RuntimeHotStateRecord>()
                .ToArray();
        }

        // Instances materialized by an earlier package version retain their owner sets.
        // Keep that migration path out of the new hot path while allowing a rolling upgrade.
        var entityTypes = await database.SetMembersAsync(OwnerTypesKey(instanceId));
        if (entityTypes.Length == 0) return [];
        var reads = entityTypes.Select(type =>
            ReadByInstanceAsync(type.ToString(), instanceId, cancellationToken));
        var records = await Task.WhenAll(reads);
        return records.SelectMany(items => items).ToArray();
    }

    public async Task<RuntimeHotStateLeaseGraph?> TryAcquireLeaseWithGraphAsync(
        Id instanceId,
        string leaseId,
        TimeSpan duration,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var database = await GetDatabase(cancellationToken);
        var cached = _localGraphs.GetValueOrDefault(instanceId);
        var result = await database.ScriptEvaluateAsync(
            AcquireLeaseGraphScript,
            [LeaseKey(instanceId), GraphKey(instanceId)],
            [
                leaseId,
                Math.Max(1L, (long)duration.TotalMilliseconds),
                cached?.Version ?? -1L
            ]);
        cancellationToken.ThrowIfCancellationRequested();
        var values = (RedisResult[]?)result;
        if (values is null || values.Length == 0 || (long)values[0] == 0) return null;

        var version = long.TryParse(values[1].ToString(), out var parsedVersion)
            ? parsedVersion
            : 0L;
        if (values.Length == 2 && cached is not null && cached.Version == version)
            return new RuntimeHotStateLeaseGraph(version, cached.Records);

        var records = values.Skip(2)
            .Select(value => (RedisValue)value.ToString())
            .Select(Materialize)
            .Where(record => record is not null)
            .Cast<RuntimeHotStateRecord>()
            .ToArray();
        StoreLocalGraph(instanceId, version, records);
        return new RuntimeHotStateLeaseGraph(version, records);
    }

    public async Task<RuntimeHotStateLeaseGraph> ReadGraphFenceAsync(
        Id instanceId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var cached = _localGraphs.GetValueOrDefault(instanceId);
        if (cached is not null)
            return new RuntimeHotStateLeaseGraph(cached.Version, cached.Records, true);

        var database = await GetDatabase(cancellationToken);
        var entries = await database.HashGetAllAsync(GraphKey(instanceId));
        cancellationToken.ThrowIfCancellationRequested();
        var versionValue = entries.FirstOrDefault(entry => entry.Name == GraphVersionField).Value;
        var version = long.TryParse(versionValue.ToString(), out var parsedVersion)
            ? parsedVersion
            : 0L;
        var records = entries
            .Where(entry => entry.Name != GraphVersionField &&
                !entry.Name.ToString().StartsWith('@'))
            .Select(entry => Materialize(entry.Value))
            .Where(record => record is not null)
            .Cast<RuntimeHotStateRecord>()
            .ToArray();
        StoreLocalGraph(instanceId, version, records);
        return new RuntimeHotStateLeaseGraph(version, records, true);
    }

    public async Task RecordDispatchPublicationAsync(
        Id dispatchId,
        DateTime publishedOnUtc,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var database = await GetDatabase(cancellationToken);
        await database.StringSetAsync(
            DispatchReceiptKey(dispatchId),
            publishedOnUtc.ToUniversalTime().Ticks,
            TimeSpan.FromDays(7),
            When.NotExists);
        cancellationToken.ThrowIfCancellationRequested();
    }

    public async Task<DateTime?> TryReadDispatchPublicationAsync(
        Id dispatchId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var database = await GetDatabase(cancellationToken);
        var value = await database.StringGetAsync(DispatchReceiptKey(dispatchId));
        cancellationToken.ThrowIfCancellationRequested();
        return value.TryParse(out long ticks)
            ? new DateTime(ticks, DateTimeKind.Utc)
            : null;
    }

    public async Task<RuntimeHotStateRecord?> TryReadByIndexAsync(string entityType, string indexName, string indexValue, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(indexValue)) return null;
        cancellationToken.ThrowIfCancellationRequested();
        var database = await GetDatabase(cancellationToken);
        var digest = BuildLookupDigest(entityType, indexName, indexValue);
        var id = await database.HashGetAsync(LookupsKey, digest);
        if (id.IsNullOrEmpty)
            id = await database.StringGetAsync(LookupKey(digest));
        return id.IsNullOrEmpty ? null : await TryReadAsync(entityType, id.ToString(), cancellationToken);
    }

    public async Task<IReadOnlyCollection<RuntimeHotStateRecord>> ReadPendingInstancesAsync(int take, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var database = await GetDatabase(cancellationToken);
        var ids = await database.SortedSetRangeByRankAsync(PendingInstancesKey, 0, Math.Max(0, take - 1), Order.Descending);
        if (ids.Length == 0) return [];
        return await ReadManyAsync(database, InstanceEntityType, ids, cancellationToken);
    }

    public async Task<bool> TryAcquireLeaseAsync(Id instanceId, string leaseId, TimeSpan duration, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var database = await GetDatabase(cancellationToken);
        var leaseKey = LeaseKey(instanceId);
        var leaseDuration = TimeSpan.FromMilliseconds(Math.Max(1L, (long)duration.TotalMilliseconds));
        if (await database.StringSetAsync(leaseKey, leaseId, leaseDuration, When.NotExists))
            return true;

        // Contention and renewal are uncommon. Keep their compare/refresh atomic without
        // making every uncontended instance acquisition execute on Redis' Lua thread.
        var result = await database.ScriptEvaluateAsync(AcquireLeaseScript, [leaseKey],
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
            var leaseSize = Math.Min(
                Math.Max(1, take),
                Math.Max(1, _options.MaxProjectionLeaseSize));
            var sequences = await database.SortedSetRangeByRankAsync(
                PendingKey, 0, leaseSize - 1, Order.Ascending);
            if (sequences.Length == 0)
            {
                await ReleaseProjectionLock(database, token);
                return null;
            }
            var batches = new List<RuntimeHotProjectionBatch>(sequences.Length);
            var readChunkSize = Math.Min(
                sequences.Length,
                Math.Max(1, _options.ProjectionReadChunkSize));
            foreach (var sequenceChunk in sequences.Chunk(readChunkSize))
            {
                var chunk = sequenceChunk.ToArray();
                var payloads = await database.HashGetAsync(JournalKey, chunk);
                var referencesBySequence = new List<(long Sequence, RuntimeHotJournalReference[] References)>(chunk.Length);
                for (var index = 0; index < chunk.Length; index++)
                {
                    if (payloads[index].IsNullOrEmpty) continue;
                    var references = JsonSerializer.Deserialize<RuntimeHotJournalReference[]>(
                        payloads[index].ToString(), JsonOptions)
                        ?? [];
                    referencesBySequence.Add((long.Parse(chunk[index].ToString()), references));
                }

                var recordsByReference = new Dictionary<RuntimeHotJournalReference, RuntimeHotStateRecord>();
                foreach (var group in referencesBySequence
                    .SelectMany(item => item.References)
                    .Distinct()
                    .GroupBy(reference => reference.EntityType, StringComparer.Ordinal))
                {
                    var groupReferences = group.ToArray();
                    var records = await ReadManyAsync(
                        database,
                        group.Key,
                        groupReferences.Select(reference => (RedisValue)reference.EntityId).ToArray(),
                        cancellationToken);
                    foreach (var record in records)
                    {
                        recordsByReference[new RuntimeHotJournalReference(
                            record.EntityType,
                            record.EntityId)] = record;
                    }
                }

                foreach (var journal in referencesBySequence)
                {
                    var mutations = journal.References
                        .Select(reference => recordsByReference.GetValueOrDefault(reference))
                        .Where(record => record is not null)
                        .Select(record => new RuntimeHotStateMutation(
                            record!.EntityType,
                            record.EntityId,
                            record.InstanceId,
                            record.Operation,
                            record.Payload))
                        .ToArray();
                    batches.Add(new RuntimeHotProjectionBatch(journal.Sequence, mutations));
                }
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
        var cleanupSize = Math.Min(
            Math.Max(1, take),
            Math.Max(1, _options.TerminalCleanupBatchSize));
        var result = await database.ScriptEvaluateAsync(CleanupProjectedTerminalScript,
            [ProjectedTerminalKey, PendingInstancesKey, LookupsKey],
            [Root, cleanupSize, InstanceEntityType]);
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

    private RedisValue[] BuildCommitValues(
        IReadOnlyCollection<RuntimeHotStateMutation> mutations,
        string journalPayload,
        string? leaseId,
        long? expectedVersion)
    {
        var indexCount = mutations.Sum(mutation => mutation.Indexes?.Count ?? 0);
        var values = new List<RedisValue>(6 + (mutations.Count * 6) + (indexCount * 3))
        {
            journalPayload,
            Root,
            InstanceEntityType,
            mutations.Count,
            leaseId ?? string.Empty,
            expectedVersion?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty
        };
        foreach (var mutation in mutations)
        {
            values.Add(mutation.EntityType);
            values.Add(mutation.EntityId);
            values.Add(mutation.InstanceId ?? string.Empty);
            values.Add((int)mutation.Operation);
            values.Add(EncodePayload(mutation.Payload));
            values.Add(mutation.Indexes?.Count ?? 0);
            if (mutation.Indexes is null) continue;
            foreach (var index in mutation.Indexes.OrderBy(item => item.Key, StringComparer.Ordinal))
            {
                values.Add(index.Key);
                values.Add(index.Value);
                values.Add(BuildLookupDigest(mutation.EntityType, index.Key, index.Value));
            }
        }
        return values.ToArray();
    }

    private static string BuildLookupDigest(string type, string name, string value)
        => Convert.ToHexString(SHA1.HashData(
            Encoding.UTF8.GetBytes($"{type}|{name}|{value}"))).ToLowerInvariant();

    private void UpdateLocalGraphs(
        IReadOnlyCollection<RuntimeHotStateMutation> mutations,
        long sequence)
    {
        foreach (var group in mutations
            .Where(mutation => !string.IsNullOrWhiteSpace(mutation.InstanceId))
            .GroupBy(mutation => new Id(Ulid.Parse(mutation.InstanceId!))))
        {
            while (true)
            {
                if (!_localGraphs.TryGetValue(group.Key, out var current))
                {
                    var initialRecords = group.Select(mutation => new RuntimeHotStateRecord(
                        mutation.EntityType,
                        mutation.EntityId,
                        mutation.InstanceId,
                        mutation.Operation,
                        mutation.Payload,
                        sequence)).ToArray();
                    var initial = new LocalGraph(
                        sequence,
                        initialRecords,
                        Interlocked.Increment(ref _localGraphGeneration));
                    if (_localGraphs.TryAdd(group.Key, initial)) break;
                    continue;
                }
                if (current.Version > sequence) break;
                var records = current.Records.ToDictionary(
                    record => $"{record.EntityType}{RecordSeparator}{record.EntityId}",
                    StringComparer.Ordinal);
                foreach (var mutation in group)
                {
                    records[$"{mutation.EntityType}{RecordSeparator}{mutation.EntityId}"] =
                        new RuntimeHotStateRecord(
                            mutation.EntityType,
                            mutation.EntityId,
                            mutation.InstanceId,
                            mutation.Operation,
                            mutation.Payload,
                            sequence);
                }

                var updated = new LocalGraph(
                    sequence,
                    records.Values.ToArray(),
                    Interlocked.Increment(ref _localGraphGeneration));
                if (_localGraphs.TryUpdate(group.Key, updated, current)) break;
            }
        }
    }

    private void StoreLocalGraph(
        Id instanceId,
        long version,
        IReadOnlyCollection<RuntimeHotStateRecord> records)
    {
        _localGraphs[instanceId] = new LocalGraph(
            version,
            records,
            Interlocked.Increment(ref _localGraphGeneration));
        if (_localGraphs.Count <= MaximumLocalGraphs) return;

        var oldest = _localGraphs
            .OrderBy(candidate => candidate.Value.Generation)
            .Take(Math.Max(1, _localGraphs.Count - MaximumLocalGraphs))
            .ToArray();
        foreach (var candidate in oldest)
            ((ICollection<KeyValuePair<Id, LocalGraph>>)_localGraphs).Remove(candidate);
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

    private async Task<IReadOnlyCollection<RuntimeHotStateRecord>> ReadManyAsync(
        IDatabase database,
        string entityType,
        RedisValue[] ids,
        CancellationToken cancellationToken)
    {
        var keys = ids.Select(id => EntityKey(entityType, id.ToString())).ToArray();
        var values = await database.StringGetAsync(keys);
        cancellationToken.ThrowIfCancellationRequested();
        var records = new List<RuntimeHotStateRecord>(values.Length);
        List<Task<RuntimeHotStateRecord?>>? legacyReads = null;
        for (var index = 0; index < values.Length; index++)
        {
            var record = Materialize(values[index]);
            if (record is not null)
            {
                records.Add(record);
                continue;
            }

            legacyReads ??= [];
            legacyReads.Add(TryReadLegacyHashAsync(
                database,
                entityType,
                ids[index].ToString(),
                cancellationToken));
        }

        if (legacyReads is not null)
        {
            var legacy = await Task.WhenAll(legacyReads);
            foreach (var record in legacy)
            {
                if (record is not null) records.Add(record);
            }
        }
        return records;
    }

    private async Task<RuntimeHotStateRecord?> TryReadLegacyHashAsync(
        IDatabase database,
        string entityType,
        string entityId,
        CancellationToken cancellationToken)
    {
        var values = await database.HashGetAsync(
            EntityKey(entityType, entityId),
            ["entityType", "entityId", "instanceId", "operation", "payload", "sequence"]);
        cancellationToken.ThrowIfCancellationRequested();
        return MaterializeLegacy(values);
    }

    private RuntimeHotStateRecord? Materialize(RedisValue value)
    {
        if (value.IsNullOrEmpty) return null;
        var serialized = value.ToString();
        Span<Range> ranges = stackalloc Range[6];
        if (serialized.AsSpan().Split(ranges, RecordSeparator) != 6) return null;
        if (!int.TryParse(serialized[ranges[3]], out var operation) ||
            !long.TryParse(serialized[ranges[4]], out var sequence))
            return null;
        var instanceId = serialized[ranges[2]];
        return new RuntimeHotStateRecord(
            serialized[ranges[0]],
            serialized[ranges[1]],
            instanceId.Length == 0 ? null : instanceId,
            (RuntimeHotStateOperation)operation,
            DecodePayload(serialized[ranges[5]]),
            sequence);
    }

    private string EncodePayload(string payload)
    {
        if (_options.PayloadCompressionThresholdBytes == 0 ||
            payload.Length < _options.PayloadCompressionThresholdBytes)
            return payload;
        var input = Encoding.UTF8.GetBytes(payload);
        if (input.Length < _options.PayloadCompressionThresholdBytes) return payload;
        var compressed = new byte[BrotliEncoder.GetMaxCompressedLength(input.Length)];
        if (!BrotliEncoder.TryCompress(input, compressed, out var written, quality: 1, window: 20) ||
            written >= input.Length)
            return payload;
        return CompressedPayloadPrefix + Convert.ToBase64String(compressed, 0, written);
    }

    private static string DecodePayload(string payload)
    {
        if (!payload.StartsWith(CompressedPayloadPrefix, StringComparison.Ordinal)) return payload;
        var compressed = Convert.FromBase64String(payload[CompressedPayloadPrefix.Length..]);
        using var input = new MemoryStream(compressed, writable: false);
        using var brotli = new BrotliStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        brotli.CopyTo(output);
        return Encoding.UTF8.GetString(output.GetBuffer(), 0, checked((int)output.Length));
    }

    private static RuntimeHotStateRecord? MaterializeLegacy(RedisValue[] values)
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
    private RedisKey LookupsKey => $"{Root}:lookups";
    private RedisKey EntityKey(string type, string id) => $"{Root}:entity:{type}:{id}";
    private RedisKey GraphKey(Id instanceId) => $"{Root}:graph:{instanceId}";
    private RedisKey DispatchReceiptKey(Id dispatchId) => $"{Root}:dispatch-receipt:{dispatchId}";
    private RedisKey OwnerKey(Id instanceId, string type) => $"{Root}:owner:{instanceId}:{type}";
    private RedisKey OwnerTypesKey(Id instanceId) => $"{Root}:owner-types:{instanceId}";
    private RedisKey LeaseKey(Id instanceId) => $"{Root}:lease:{instanceId}";
    private RedisKey LookupKey(string type, string name, string value)
    {
        var digest = BuildLookupDigest(type, name, value);
        return LookupKey(digest);
    }
    private RedisKey LookupKey(string digest) => $"{Root}:lookup:{digest}";

    public async ValueTask DisposeAsync()
    {
        if (_ownsConnection && _connection is not null) await _connection.DisposeAsync();
        _connectionGate.Dispose();
    }

    private sealed record RuntimeHotJournalReference(string EntityType, string EntityId);
    private sealed record LocalGraph(
        long Version,
        IReadOnlyCollection<RuntimeHotStateRecord> Records,
        long Generation);
}
