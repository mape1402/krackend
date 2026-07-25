using System.Data;
using System.Data.Common;
using Krackend.EventSourcing.Configuration;
using Krackend.EventSourcing.Envelopes;

namespace Krackend.EventSourcing.Stores.Relational;

/// <summary>
/// Relational event store implementation backed by ADO.NET.
/// </summary>
public sealed class RelationalEventStore : IEventStore, IEventLogReader
{
    private readonly EventStoreOptionsCollection _stores;
    private readonly IRelationalEventStoreConnectionFactory _connectionFactory;
    private readonly IEventStoreSqlDialect _sqlDialect;
    private readonly IEventEnvelopeFactory _envelopeFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="RelationalEventStore"/> class.
    /// </summary>
    public RelationalEventStore(
        EventStoreOptionsCollection stores,
        IRelationalEventStoreConnectionFactory connectionFactory,
        IEventStoreSqlDialect sqlDialect,
        IEventEnvelopeFactory envelopeFactory)
    {
        _stores = stores ?? throw new ArgumentNullException(nameof(stores));
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _sqlDialect = sqlDialect ?? throw new ArgumentNullException(nameof(sqlDialect));
        _envelopeFactory = envelopeFactory ?? throw new ArgumentNullException(nameof(envelopeFactory));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<EventEnvelope>> LoadAsync(
        string streamName,
        string streamId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(streamName);
        ArgumentException.ThrowIfNullOrWhiteSpace(streamId);

        var store = _stores.GetRequired(streamName);
        await using var connection = await CreateOpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = _sqlDialect.LoadStreamSql(store.QualifiedTableName);
        AddParameter(command, "@StreamName", streamName);
        AddParameter(command, "@StreamId", streamId);

        var envelopes = new List<EventEnvelope>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            envelopes.Add(ReadEnvelope(reader));
        }

        return envelopes;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<EventEnvelope>> AppendAsync(
        string streamName,
        string streamId,
        long expectedVersion,
        IReadOnlyCollection<object> events,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(streamName);
        ArgumentException.ThrowIfNullOrWhiteSpace(streamId);
        ArgumentNullException.ThrowIfNull(events);

        if (expectedVersion < 0)
            throw new ArgumentOutOfRangeException(nameof(expectedVersion), "Expected version cannot be negative.");

        var store = _stores.GetRequired(streamName);
        await using var connection = await CreateOpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        var actualVersion = await GetActualVersionAsync(connection, transaction, store, streamName, streamId, cancellationToken);

        if (actualVersion != expectedVersion)
            throw new EventStoreConcurrencyException(streamName, streamId, expectedVersion, actualVersion);

        var pendingEnvelopes = _envelopeFactory.Create(streamName, streamId, null, expectedVersion, events);
        var committedEnvelopes = new List<EventEnvelope>(pendingEnvelopes.Count);

        foreach (var envelope in pendingEnvelopes)
        {
            var globalPosition = await InsertAsync(connection, transaction, store, envelope, cancellationToken);
            committedEnvelopes.Add(envelope with { GlobalPosition = globalPosition });
        }

        await transaction.CommitAsync(cancellationToken);

        return committedEnvelopes;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<EventEnvelope>> ReadFromAsync(
        string streamName,
        long afterGlobalPosition,
        int maxCount,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(streamName);

        if (afterGlobalPosition < 0)
            throw new ArgumentOutOfRangeException(nameof(afterGlobalPosition), "Global position cannot be negative.");

        if (maxCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxCount), "Max count must be greater than zero.");

        var store = _stores.GetRequired(streamName);
        await using var connection = await CreateOpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = _sqlDialect.ReadFromSql(store.QualifiedTableName);
        AddParameter(command, "@StreamName", streamName);
        AddParameter(command, "@AfterGlobalPosition", afterGlobalPosition);
        AddParameter(command, "@MaxCount", maxCount);

        var envelopes = new List<EventEnvelope>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            envelopes.Add(ReadEnvelope(reader));
        }

        return envelopes;
    }

    private async Task<long> GetActualVersionAsync(
        DbConnection connection,
        DbTransaction transaction,
        EventStoreOptions store,
        string streamName,
        string streamId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = _sqlDialect.SelectStreamVersionSql(store.QualifiedTableName);
        AddParameter(command, "@StreamName", streamName);
        AddParameter(command, "@StreamId", streamId);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt64(result);
    }

    private async Task<long> InsertAsync(
        DbConnection connection,
        DbTransaction transaction,
        EventStoreOptions store,
        EventEnvelope envelope,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = _sqlDialect.InsertEventSql(store.QualifiedTableName);

        AddParameter(command, "@EventId", envelope.EventId);
        AddParameter(command, "@StreamName", envelope.StreamName);
        AddParameter(command, "@StreamId", envelope.StreamId);
        AddParameter(command, "@StreamType", envelope.StreamType);
        AddParameter(command, "@StreamVersion", envelope.StreamVersion);
        AddParameter(command, "@EventType", envelope.EventType);
        AddParameter(command, "@EventVersion", envelope.EventVersion);
        AddParameter(command, "@OccurredAt", envelope.OccurredAt);
        AddParameter(command, "@Payload", envelope.Payload);
        AddParameter(command, "@Metadata", envelope.Metadata);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt64(result);
    }

    private async ValueTask<DbConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);

        return connection;
    }

    private static EventEnvelope ReadEnvelope(DbDataReader reader)
        => new(
            EventId: reader.GetGuid(reader.GetOrdinal("EventId")),
            StreamName: reader.GetString(reader.GetOrdinal("StreamName")),
            StreamId: reader.GetString(reader.GetOrdinal("StreamId")),
            StreamType: ReadNullableString(reader, "StreamType"),
            StreamVersion: reader.GetInt64(reader.GetOrdinal("StreamVersion")),
            GlobalPosition: reader.GetInt64(reader.GetOrdinal("GlobalPosition")),
            EventType: reader.GetString(reader.GetOrdinal("EventType")),
            EventVersion: reader.GetInt32(reader.GetOrdinal("EventVersion")),
            OccurredAt: reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("OccurredAt")),
            Payload: reader.GetString(reader.GetOrdinal("Payload")),
            Metadata: ReadNullableString(reader, "Metadata"));

    private static string? ReadNullableString(DbDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    private static void AddParameter(DbCommand command, string name, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }
}
