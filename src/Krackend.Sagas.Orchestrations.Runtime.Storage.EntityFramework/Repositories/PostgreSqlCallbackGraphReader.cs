using System.Data;
using System.Text.Json;
using System.Text.Json.Serialization;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Repositories;

/// <summary>
/// Reads the PostgreSQL callback graph as one compact row. The regular joined EF
/// query repeats the instance, callback task and callback stage once for every
/// stage/task combination. That is harmless at low traffic but becomes the largest
/// runtime query during callback bursts. PostgreSQL aggregates the small child sets
/// while retaining one round trip and the entities are attached as normal tracked
/// state before the engine mutates them.
/// </summary>
internal static class PostgreSqlCallbackGraphReader
{
    private const string Sql = """
        SELECT
            row_to_json(instance_row)::text,
            row_to_json(callback_task)::text,
            row_to_json(callback_stage)::text,
            (
                SELECT jsonb_agg(to_jsonb(graph_stage) ORDER BY graph_stage."Order")
                FROM "Runtime"."StageExecutions" AS graph_stage
                WHERE graph_stage."OrchestrationInstanceId" = instance_row."Id"
                    AND graph_stage."Id" <> callback_stage."Id"
            )::text,
            (
                SELECT jsonb_agg(to_jsonb(stage_task) ORDER BY stage_task."TaskKey")
                FROM "Runtime"."TaskExecutions" AS stage_task
                WHERE stage_task."StageExecutionId" = callback_task."StageExecutionId"
                    AND stage_task."Id" <> callback_task."Id"
            )::text
        FROM "Runtime"."TaskExecutions" AS callback_task
        INNER JOIN "Runtime"."OrchestrationInstances" AS instance_row
            ON callback_task."OrchestrationInstanceId" = instance_row."Id"
        INNER JOIN "Runtime"."StageExecutions" AS callback_stage
            ON callback_task."StageExecutionId" = callback_stage."Id"
        WHERE callback_task."Id" = @dispatchId;
        """;

    private static readonly JsonSerializerOptions SerializerOptions =
        CallbackGraphJsonMaterializer.CreateSerializerOptions(new PostgreSqlIdJsonConverter());

    public static async Task<TaskDispatchCallbackExecutionState> TryReadAsync(
        RuntimeDbContext dbContext,
        Id dispatchId,
        CancellationToken cancellationToken)
    {
        string instanceJson = null;
        string callbackTaskJson = null;
        string callbackStageJson = null;
        string stagesJson = null;
        string stageTasksJson = null;
        var connection = dbContext.Database.GetDbConnection();
        var closeConnection = connection.State != ConnectionState.Open;
        if (closeConnection)
            await dbContext.Database.OpenConnectionAsync(cancellationToken);

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = Sql;
            command.CommandType = CommandType.Text;
            command.Transaction = dbContext.Database.CurrentTransaction?.GetDbTransaction();
            var parameter = command.CreateParameter();
            parameter.ParameterName = "@dispatchId";
            parameter.DbType = DbType.Binary;
            parameter.Value = dispatchId.Value.ToByteArray();
            command.Parameters.Add(parameter);

            await using (var reader = await command.ExecuteReaderAsync(
                             CommandBehavior.SingleRow,
                             cancellationToken))
            {
                if (!await reader.ReadAsync(cancellationToken))
                    return null;

                // Copy the compact row and release the physical connector before JSON
                // materialization and EF tracking. Under callback bursts those CPU-only
                // steps otherwise hold scarce database sessions idle and serialize
                // unrelated orchestration instances behind the connection pool.
                instanceJson = reader.GetString(0);
                callbackTaskJson = reader.GetString(1);
                callbackStageJson = reader.GetString(2);
                stagesJson = reader.IsDBNull(3) ? null : reader.GetString(3);
                stageTasksJson = reader.IsDBNull(4) ? null : reader.GetString(4);
            }
        }
        finally
        {
            if (closeConnection)
                await dbContext.Database.CloseConnectionAsync();
        }

        return CallbackGraphJsonMaterializer.Materialize(
            dbContext,
            dispatchId,
            instanceJson,
            callbackTaskJson,
            callbackStageJson,
            stagesJson,
            stageTasksJson,
            SerializerOptions,
            "PostgreSQL");
    }

    private sealed class PostgreSqlIdJsonConverter : JsonConverter<Id>
    {
        public override Id Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var value = reader.GetString()
                ?? throw new JsonException("A PostgreSQL runtime id cannot be null.");
            if (!value.StartsWith("\\x", StringComparison.Ordinal) || value.Length != 34)
                throw new JsonException($"PostgreSQL runtime id '{value}' is invalid.");
            return new Id(new Ulid(Convert.FromHexString(value.AsSpan(2))));
        }

        public override void Write(Utf8JsonWriter writer, Id value, JsonSerializerOptions options)
            => writer.WriteStringValue(value.ToString());
    }

}
