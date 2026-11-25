using System.Data;
using System.Text.Json;
using System.Text.Json.Serialization;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Infrastructure;
using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.SqlServer;

internal static class SqlServerCallbackGraphReader
{
    private const string Sql = """
        SELECT
            (SELECT instance_row.* FOR JSON PATH, WITHOUT_ARRAY_WRAPPER),
            (SELECT callback_task.* FOR JSON PATH, WITHOUT_ARRAY_WRAPPER),
            (SELECT callback_stage.* FOR JSON PATH, WITHOUT_ARRAY_WRAPPER),
            (
                SELECT graph_stage.*
                FROM [Runtime].[StageExecutions] AS graph_stage
                WHERE graph_stage.[OrchestrationInstanceId] = instance_row.[Id]
                    AND graph_stage.[Id] <> callback_stage.[Id]
                ORDER BY graph_stage.[Order]
                FOR JSON PATH
            ),
            (
                SELECT stage_task.*
                FROM [Runtime].[TaskExecutions] AS stage_task
                WHERE stage_task.[StageExecutionId] = callback_task.[StageExecutionId]
                    AND stage_task.[Id] <> callback_task.[Id]
                ORDER BY stage_task.[TaskKey]
                FOR JSON PATH
            )
        FROM [Runtime].[TaskExecutions] AS callback_task
        INNER JOIN [Runtime].[OrchestrationInstances] AS instance_row
            ON callback_task.[OrchestrationInstanceId] = instance_row.[Id]
        INNER JOIN [Runtime].[StageExecutions] AS callback_stage
            ON callback_task.[StageExecutionId] = callback_stage.[Id]
        WHERE callback_task.[Id] = @dispatchId;
        """;

    private static readonly JsonSerializerOptions SerializerOptions =
        CallbackGraphJsonMaterializer.CreateSerializerOptions(new SqlServerIdJsonConverter());

    public static async Task<TaskDispatchCallbackExecutionState?> TryReadAsync(
        RuntimeDbContext dbContext,
        Id dispatchId,
        CancellationToken cancellationToken)
    {
        string? instanceJson = null;
        string? callbackTaskJson = null;
        string? callbackStageJson = null;
        string? stagesJson = null;
        string? stageTasksJson = null;
        var connection = dbContext.Database.GetDbConnection();
        var closeConnection = connection.State != ConnectionState.Open;
        if (closeConnection) await dbContext.Database.OpenConnectionAsync(cancellationToken);

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

            await using var reader = await command.ExecuteReaderAsync(
                CommandBehavior.SingleRow,
                cancellationToken);
            if (!await reader.ReadAsync(cancellationToken)) return null;
            instanceJson = reader.GetString(0);
            callbackTaskJson = reader.GetString(1);
            callbackStageJson = reader.GetString(2);
            stagesJson = reader.IsDBNull(3) ? null : reader.GetString(3);
            stageTasksJson = reader.IsDBNull(4) ? null : reader.GetString(4);
        }
        finally
        {
            if (closeConnection) await dbContext.Database.CloseConnectionAsync();
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
            "SQL Server");
    }

    private sealed class SqlServerIdJsonConverter : JsonConverter<Id>
    {
        public override Id Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var value = reader.GetString()
                ?? throw new JsonException("A SQL Server runtime id cannot be null.");
            var bytes = Convert.FromBase64String(value);
            if (bytes.Length != 16)
                throw new JsonException("A SQL Server runtime id must contain 16 bytes.");
            return new Id(new Ulid(bytes));
        }

        public override void Write(Utf8JsonWriter writer, Id value, JsonSerializerOptions options)
            => writer.WriteBase64StringValue(value.Value.ToByteArray());
    }
}
