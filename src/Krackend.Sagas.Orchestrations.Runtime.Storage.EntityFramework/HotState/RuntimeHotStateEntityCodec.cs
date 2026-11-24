#nullable enable

namespace Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.HotState;

using System.Text.Json;
using System.Text.Json.Serialization;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Infrastructure;
using Microsoft.EntityFrameworkCore.ChangeTracking;

internal static class RuntimeHotStateEntityCodec
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private static readonly IReadOnlyDictionary<string, Type> Types = new[]
    {
        typeof(OrchestrationInstance), typeof(StageExecution), typeof(TaskExecution),
        typeof(TaskExecutionAttempt), typeof(TaskDispatch), typeof(ExecutionTransition),
        typeof(InstanceVariable), typeof(CompensationExecution), typeof(SagaStatisticsObservation)
    }.ToDictionary(type => type.FullName!, StringComparer.Ordinal);

    public static bool IsSupported(Type type) => Types.ContainsKey(type.FullName ?? string.Empty);

    public static RuntimeHotStateMutation CreateMutation(EntityEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var entity = entry.Entity;
        var type = entity.GetType();
        if (!IsSupported(type))
            throw new NotSupportedException($"Entity '{type.FullName}' is not supported by runtime hot state.");
        var primaryKey = entry.Metadata.FindPrimaryKey()
            ?? throw new InvalidOperationException($"Entity '{type.FullName}' has no primary key.");
        var id = string.Join("|", primaryKey.Properties.Select(property =>
            Convert.ToString(entry.Property(property.Name).CurrentValue, System.Globalization.CultureInfo.InvariantCulture)));
        var instanceId = ResolveInstanceId(entry);
        return new RuntimeHotStateMutation(
            type.FullName!,
            id,
            instanceId,
            entry.State == Microsoft.EntityFrameworkCore.EntityState.Deleted
                ? RuntimeHotStateOperation.Delete
                : RuntimeHotStateOperation.Upsert,
            JsonSerializer.Serialize(entity, type, JsonOptions),
            ResolveIndexes(entity));
    }

    public static RuntimeHotStateMutation CreateCacheMutation<T>(T entity)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(entity);
        var type = entity.GetType();
        var id = entity switch
        {
            OrchestrationInstance value => value.Id.ToString(),
            StageExecution value => value.Id.ToString(),
            TaskExecution value => value.Id.ToString(),
            TaskExecutionAttempt value => value.Id.ToString(),
            TaskDispatch value => value.Id.ToString(),
            ExecutionTransition value => value.Id.ToString(),
            InstanceVariable value => value.Id.ToString(),
            CompensationExecution value => value.Id.ToString(),
            SagaStatisticsObservation value => value.Id,
            _ => throw new NotSupportedException($"Entity '{type.FullName}' is not supported by runtime hot state.")
        };
        return new RuntimeHotStateMutation(type.FullName!, id, ResolveInstanceId(entity),
            RuntimeHotStateOperation.Upsert, JsonSerializer.Serialize(entity, type, JsonOptions), ResolveIndexes(entity));
    }

    public static T? Deserialize<T>(RuntimeHotStateRecord? record) where T : class
        => record is null || record.Operation == RuntimeHotStateOperation.Delete
            ? null
            : JsonSerializer.Deserialize<T>(record.Payload, JsonOptions);

    public static object Deserialize(RuntimeHotStateMutation mutation)
    {
        if (!Types.TryGetValue(mutation.EntityType, out var type))
            throw new InvalidOperationException($"Unknown hot-state entity type '{mutation.EntityType}'.");
        return JsonSerializer.Deserialize(mutation.Payload, type, JsonOptions)
            ?? throw new InvalidOperationException($"Hot-state payload for '{mutation.EntityType}' was empty.");
    }

    public static Type ResolveType(string entityType)
        => Types.TryGetValue(entityType, out var type)
            ? type
            : throw new InvalidOperationException($"Unknown hot-state entity type '{entityType}'.");

    private static string? ResolveInstanceId(object entity) => entity switch
    {
        OrchestrationInstance value => value.Id.ToString(),
        StageExecution value => value.OrchestrationInstanceId.ToString(),
        TaskExecution value => value.OrchestrationInstanceId.ToString(),
        ExecutionTransition value => value.OrchestrationInstanceId.ToString(),
        InstanceVariable value => value.OrchestrationInstanceId.ToString(),
        CompensationExecution value => value.OrchestrationInstanceId.ToString(),
        SagaStatisticsObservation value => value.InstanceId.ToString(),
        _ => null
    };

    private static string? ResolveInstanceId(EntityEntry entry)
    {
        var direct = ResolveInstanceId(entry.Entity);
        if (direct is not null) return direct;

        // Attempts and dispatches do not repeat the instance FK in the domain
        // model. Their parent graph is already tracked in every engine activation,
        // so derive ownership locally without putting a SQL lookup in the hot path.
        if (entry.Entity is TaskExecutionAttempt attempt)
            return entry.Context.Set<TaskExecution>().Local
                .FirstOrDefault(task => task.Id == attempt.TaskExecutionId)
                ?.OrchestrationInstanceId.ToString();
        if (entry.Entity is TaskDispatch dispatch)
        {
            var attemptOwner = entry.Context.Set<TaskExecutionAttempt>().Local
                .FirstOrDefault(item => item.Id == dispatch.TaskExecutionAttemptId);
            if (attemptOwner is null) return null;
            return entry.Context.Set<TaskExecution>().Local
                .FirstOrDefault(task => task.Id == attemptOwner.TaskExecutionId)
                ?.OrchestrationInstanceId.ToString();
        }
        return null;
    }

    private static IReadOnlyDictionary<string, string>? ResolveIndexes(object entity)
    {
        var indexes = new Dictionary<string, string>(StringComparer.Ordinal);
        switch (entity)
        {
            case OrchestrationInstance value:
                Add(indexes, "start", value.StartIdempotencyKey);
                Add(indexes, "correlation", value.CorrelationId);
                Add(indexes, "saga", value.SagaId);
                Add(indexes, "execution", value.ExecutionKey);
                break;
            case StageExecution value:
                Add(indexes, "instance-stage", $"{value.OrchestrationInstanceId}|{value.StageKey}");
                break;
            case TaskExecution value:
                Add(indexes, "correlation", value.CorrelationId);
                Add(indexes, "stage-task", $"{value.StageExecutionId}|{value.TaskKey}");
                break;
            case TaskExecutionAttempt value:
                if (value.DispatchId is { } dispatchId) Add(indexes, "dispatch", dispatchId.ToString());
                Add(indexes, "task-attempt", $"{value.TaskExecutionId}|{value.AttemptNumber}");
                break;
            case TaskDispatch value:
                Add(indexes, "command", value.CommandId);
                Add(indexes, "attempt", value.TaskExecutionAttemptId.ToString());
                break;
        }
        return indexes.Count == 0 ? null : indexes;
    }

    private static void Add(IDictionary<string, string> indexes, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value)) indexes[name] = value;
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new IdJsonConverter());
        return options;
    }

    private sealed class IdJsonConverter : JsonConverter<Id>
    {
        public override Id Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            => Ulid.TryParse(reader.GetString(), out var value)
                ? new Id(value)
                : throw new JsonException("Invalid runtime identifier.");

        public override void Write(Utf8JsonWriter writer, Id value, JsonSerializerOptions options)
            => writer.WriteStringValue(value.ToString());
    }
}
