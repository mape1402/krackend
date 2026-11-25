using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Repositories;

public static class CallbackGraphJsonMaterializer
{
    public static JsonSerializerOptions CreateSerializerOptions(JsonConverter<Id> idConverter)
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true
        };
        options.Converters.Add(new JsonStringEnumConverter());
        options.Converters.Add(idConverter);
        options.Converters.Add(new StoredJsonNodeConverter());
        options.Converters.Add(new StoredJsonNodeDictionaryConverter());
        return options;
    }

    public static TaskDispatchCallbackExecutionState Materialize(
        RuntimeDbContext dbContext,
        Id dispatchId,
        string instanceJson,
        string callbackTaskJson,
        string callbackStageJson,
        string stagesJson,
        string stageTasksJson,
        JsonSerializerOptions serializerOptions,
        string providerName)
    {
        var instance = ResolveTracked(
            dbContext.OrchestrationInstances,
            Deserialize<OrchestrationInstance>(instanceJson, serializerOptions, providerName));
        var callbackTask = ResolveTracked(
            dbContext.TaskExecutions,
            Deserialize<TaskExecution>(callbackTaskJson, serializerOptions, providerName));
        var callbackStage = ResolveTracked(
            dbContext.StageExecutions,
            Deserialize<StageExecution>(callbackStageJson, serializerOptions, providerName));
        var stages = ReadArray<StageExecution>(stagesJson, serializerOptions)
            .Select(stage => ResolveTracked(dbContext.StageExecutions, stage))
            .Append(callbackStage)
            .GroupBy(stage => stage.Id)
            .Select(group => group.Last())
            .OrderBy(stage => stage.Order)
            .ToArray();
        var stageTasks = ReadArray<TaskExecution>(stageTasksJson, serializerOptions)
            .Select(task => ResolveTracked(dbContext.TaskExecutions, task))
            .Append(callbackTask)
            .GroupBy(task => task.Id)
            .Select(group => group.Last())
            .ToArray();

        if (!EmbeddedTaskTransportState.TryReadAttempt(callbackTask, out var attempt) ||
            !EmbeddedTaskTransportState.TryReadDispatch(callbackTask, out var dispatch))
            throw new RuntimeStateConflictException(
                $"Task '{dispatchId}' is missing its embedded transport intent.");

        AttachIfDetached(dbContext, instance);
        AttachIfDetached(dbContext, callbackTask);
        AttachIfDetached(dbContext, callbackStage);
        foreach (var stage in stages.Where(stage => stage.Id != callbackStage.Id))
            AttachIfDetached(dbContext, stage);
        foreach (var task in stageTasks.Where(task => task.Id != callbackTask.Id))
            AttachIfDetached(dbContext, task);

        return new TaskDispatchCallbackExecutionState(
            new TaskDispatchExecutionState(
                instance,
                callbackTask,
                attempt,
                dispatch,
                callbackStage),
            new OrchestrationExecutionGraph(stages, stageTasks));
    }

    private static T Deserialize<T>(
        string json,
        JsonSerializerOptions serializerOptions,
        string providerName)
        => JsonSerializer.Deserialize<T>(json, serializerOptions)
            ?? throw new JsonException($"{providerName} returned an empty {typeof(T).Name} callback row.");

    private static IReadOnlyCollection<T> ReadArray<T>(string json, JsonSerializerOptions serializerOptions)
        => string.IsNullOrWhiteSpace(json)
            ? Array.Empty<T>()
            : JsonSerializer.Deserialize<T[]>(json, serializerOptions) ?? Array.Empty<T>();

    private static TEntity ResolveTracked<TEntity>(DbSet<TEntity> set, TEntity materialized)
        where TEntity : class
    {
        var id = materialized switch
        {
            OrchestrationInstance instance => instance.Id,
            StageExecution stage => stage.Id,
            TaskExecution task => task.Id,
            _ => throw new InvalidOperationException(
                $"Unsupported compact callback entity type '{typeof(TEntity).Name}'.")
        };

        return set.Local.FirstOrDefault(candidate => candidate switch
        {
            OrchestrationInstance instance => instance.Id == id,
            StageExecution stage => stage.Id == id,
            TaskExecution task => task.Id == id,
            _ => false
        }) ?? materialized;
    }

    private static void AttachIfDetached<TEntity>(RuntimeDbContext dbContext, TEntity entity)
        where TEntity : class
    {
        if (dbContext.Entry(entity).State == EntityState.Detached)
            dbContext.Attach(entity);
    }

    private sealed class StoredJsonNodeConverter : JsonConverter<JsonNode>
    {
        public override JsonNode Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
                return null;
            if (reader.TokenType != JsonTokenType.String)
                return JsonNode.Parse(ref reader);

            var stored = reader.GetString();
            if (string.IsNullOrWhiteSpace(stored))
                return null;
            if (!stored.StartsWith("br1:", StringComparison.Ordinal))
                return JsonNode.Parse(stored);

            var compressed = Convert.FromBase64String(stored[4..]);
            using var input = new MemoryStream(compressed, writable: false);
            using var decompressor = new BrotliStream(input, CompressionMode.Decompress);
            return JsonNode.Parse(decompressor);
        }

        public override void Write(Utf8JsonWriter writer, JsonNode value, JsonSerializerOptions options)
            => value.WriteTo(writer, options);
    }

    private sealed class StoredJsonNodeDictionaryConverter
        : JsonConverter<Dictionary<string, JsonNode>>
    {
        public override Dictionary<string, JsonNode> Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
                return new Dictionary<string, JsonNode>();

            JsonObject value;
            if (reader.TokenType == JsonTokenType.String)
            {
                var stored = reader.GetString();
                value = string.IsNullOrWhiteSpace(stored)
                    ? null
                    : JsonNode.Parse(stored) as JsonObject;
            }
            else
            {
                value = JsonNode.Parse(ref reader) as JsonObject;
            }

            return value?.ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value?.DeepClone(),
                    StringComparer.Ordinal)
                ?? new Dictionary<string, JsonNode>();
        }

        public override void Write(
            Utf8JsonWriter writer,
            Dictionary<string, JsonNode> value,
            JsonSerializerOptions options)
            => JsonSerializer.Serialize(writer, value, options);
    }
}
