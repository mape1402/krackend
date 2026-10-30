using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Infrastructure;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Krackend.Sagas.Orchestrations.RuntimeHost.Sample.Migrations.RuntimeStorage
{
    /// <inheritdoc />
    [DbContext(typeof(RuntimeDbContext))]
    [Migration("20261030051000_BackfillLastExecutionCursor")]
    public partial class BackfillLastExecutionCursor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            if (!migrationBuilder.ActiveProvider.Contains("SqlServer", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            migrationBuilder.Sql(
                """
                UPDATE instance
                SET
                    LastExecutedStageKey = COALESCE(lastTask.StageKey, lastStage.StageKey),
                    LastExecutedTaskKey = lastTask.TaskKey
                FROM [Runtime].[OrchestrationInstances] AS instance
                OUTER APPLY
                (
                    SELECT TOP (1)
                        stage.StageKey,
                        task.TaskKey
                    FROM [Runtime].[TaskExecutions] AS task
                    INNER JOIN [Runtime].[StageExecutions] AS stage ON stage.Id = task.StageExecutionId
                    WHERE task.OrchestrationInstanceId = instance.Id
                        AND task.WasSkipped = 0
                    ORDER BY
                        COALESCE(task.CompletedOnUtc, task.FailedOnUtc, task.TimedOutOnUtc, task.StartedOnUtc) DESC,
                        stage.[Order] DESC,
                        task.Id DESC
                ) AS lastTask
                OUTER APPLY
                (
                    SELECT TOP (1)
                        stage.StageKey
                    FROM [Runtime].[StageExecutions] AS stage
                    WHERE stage.OrchestrationInstanceId = instance.Id
                        AND stage.WasSkipped = 0
                    ORDER BY
                        COALESCE(stage.CompletedOnUtc, stage.FailedOnUtc, stage.StartedOnUtc) DESC,
                        stage.[Order] DESC,
                        stage.Id DESC
                ) AS lastStage
                WHERE (instance.LastExecutedStageKey IS NULL OR instance.LastExecutedStageKey = '')
                    AND COALESCE(lastTask.StageKey, lastStage.StageKey) IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
