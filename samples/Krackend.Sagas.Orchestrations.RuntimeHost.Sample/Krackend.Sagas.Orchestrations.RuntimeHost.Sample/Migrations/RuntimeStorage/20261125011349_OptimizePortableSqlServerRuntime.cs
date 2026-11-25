using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Krackend.Sagas.Orchestrations.RuntimeHost.Sample.Migrations.RuntimeStorage
{
    /// <inheritdoc />
    public partial class OptimizePortableSqlServerRuntime : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_TaskExecutions",
                schema: "Runtime",
                table: "TaskExecutions");

            migrationBuilder.DropIndex(
                name: "IX_TaskExecutions_LegacyWaiting_WaitingSinceUtc_Id",
                schema: "Runtime",
                table: "TaskExecutions");

            migrationBuilder.DropIndex(
                name: "IX_TaskExecutions_Waiting_ResponseDeadlineUtc_Id",
                schema: "Runtime",
                table: "TaskExecutions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TaskExecutionAttempts",
                schema: "Runtime",
                table: "TaskExecutionAttempts");

            migrationBuilder.DropIndex(
                name: "CIX_TaskExecutionAttempts_StorageSequence",
                schema: "Runtime",
                table: "TaskExecutionAttempts");

            migrationBuilder.DropIndex(
                name: "IX_TaskExecutionAttempts_DispatchId",
                schema: "Runtime",
                table: "TaskExecutionAttempts");

            migrationBuilder.DropIndex(
                name: "IX_TaskExecutionAttempts_TaskExecutionId_AttemptNumber",
                schema: "Runtime",
                table: "TaskExecutionAttempts");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TaskDispatches",
                schema: "Runtime",
                table: "TaskDispatches");

            migrationBuilder.DropIndex(
                name: "CIX_TaskDispatches_StorageSequence",
                schema: "Runtime",
                table: "TaskDispatches");

            migrationBuilder.DropIndex(
                name: "IX_TaskDispatches_CallbackPending_CallbackAcceptedOnUtc",
                schema: "Runtime",
                table: "TaskDispatches");

            migrationBuilder.DropIndex(
                name: "IX_TaskDispatches_CommandId",
                schema: "Runtime",
                table: "TaskDispatches");

            migrationBuilder.DropIndex(
                name: "IX_TaskDispatches_PhysicalCancellationPending",
                schema: "Runtime",
                table: "TaskDispatches");

            migrationBuilder.DropIndex(
                name: "IX_TaskDispatches_QueuePending_Id_ScheduledOnUtc",
                schema: "Runtime",
                table: "TaskDispatches");

            migrationBuilder.DropIndex(
                name: "IX_TaskDispatches_TaskExecutionAttemptId",
                schema: "Runtime",
                table: "TaskDispatches");

            migrationBuilder.DropIndex(
                name: "IX_SagaStatisticsObservations_Pending_InstanceId_Revision",
                schema: "Runtime",
                table: "SagaStatisticsObservations");

            migrationBuilder.DropIndex(
                name: "IX_OrchestrationInstances_Idle_IdleExpiresOnUtc_Id",
                schema: "Runtime",
                table: "OrchestrationInstances");

            migrationBuilder.DropIndex(
                name: "IX_OrchestrationInstances_Running_ExpiresOnUtc_Id",
                schema: "Runtime",
                table: "OrchestrationInstances");

            migrationBuilder.DropIndex(
                name: "IX_OrchestrationInstances_Running_StartedOnUtc_Id",
                schema: "Runtime",
                table: "OrchestrationInstances");

            migrationBuilder.AddColumn<byte>(
                name: "StorageBucket",
                schema: "Runtime",
                table: "TaskExecutions",
                type: "tinyint",
                nullable: false,
                computedColumnSql: "CONVERT(tinyint, CONVERT(int, SUBSTRING([OrchestrationInstanceId], 16, 1)) & 63)",
                stored: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_TaskExecutions",
                schema: "Runtime",
                table: "TaskExecutions",
                column: "Id")
                .Annotation("SqlServer:Clustered", false)
                .Annotation("SqlServer:FillFactor", 95);

            migrationBuilder.AddPrimaryKey(
                name: "PK_TaskExecutionAttempts",
                schema: "Runtime",
                table: "TaskExecutionAttempts",
                column: "Id")
                .Annotation("SqlServer:Clustered", false)
                .Annotation("SqlServer:FillFactor", 95);

            migrationBuilder.AddPrimaryKey(
                name: "PK_TaskDispatches",
                schema: "Runtime",
                table: "TaskDispatches",
                column: "Id")
                .Annotation("SqlServer:Clustered", false)
                .Annotation("SqlServer:FillFactor", 95);

            migrationBuilder.CreateIndex(
                name: "CIX_TaskExecutions_StorageBucket_OrchestrationInstanceId_Id",
                schema: "Runtime",
                table: "TaskExecutions",
                columns: new[] { "StorageBucket", "OrchestrationInstanceId", "Id" },
                unique: true)
                .Annotation("SqlServer:Clustered", true)
                .Annotation("SqlServer:DataCompression", DataCompressionType.Row)
                .Annotation("SqlServer:FillFactor", 95);

            migrationBuilder.CreateIndex(
                name: "IX_TaskExecutions_LegacyWaiting_WaitingSinceUtc_Id",
                schema: "Runtime",
                table: "TaskExecutions",
                columns: new[] { "WaitingSinceUtc", "Id" },
                filter: "[Status] = 'WaitingResponse' AND [ResponseDeadlineUtc] IS NULL")
                .Annotation("SqlServer:Include", new[] { "CorrelationId" });

            migrationBuilder.CreateIndex(
                name: "IX_TaskExecutions_Waiting_ResponseDeadlineUtc_Id",
                schema: "Runtime",
                table: "TaskExecutions",
                columns: new[] { "ResponseDeadlineUtc", "Id" },
                filter: "[Status] = 'WaitingResponse' AND [ResponseDeadlineUtc] IS NOT NULL")
                .Annotation("SqlServer:Include", new[] { "CorrelationId" });

            migrationBuilder.CreateIndex(
                name: "CIX_TaskExecutionAttempts_StorageSequence",
                schema: "Runtime",
                table: "TaskExecutionAttempts",
                column: "StorageSequence",
                unique: true)
                .Annotation("SqlServer:Clustered", true)
                .Annotation("SqlServer:DataCompression", DataCompressionType.Row)
                .Annotation("SqlServer:FillFactor", 95);

            migrationBuilder.CreateIndex(
                name: "IX_TaskExecutionAttempts_DispatchId",
                schema: "Runtime",
                table: "TaskExecutionAttempts",
                column: "DispatchId")
                .Annotation("SqlServer:DataCompression", DataCompressionType.Row)
                .Annotation("SqlServer:FillFactor", 95);

            migrationBuilder.CreateIndex(
                name: "IX_TaskExecutionAttempts_TaskExecutionId_AttemptNumber",
                schema: "Runtime",
                table: "TaskExecutionAttempts",
                columns: new[] { "TaskExecutionId", "AttemptNumber" },
                unique: true)
                .Annotation("SqlServer:DataCompression", DataCompressionType.Row)
                .Annotation("SqlServer:FillFactor", 95);

            migrationBuilder.CreateIndex(
                name: "CIX_TaskDispatches_StorageSequence",
                schema: "Runtime",
                table: "TaskDispatches",
                column: "StorageSequence",
                unique: true)
                .Annotation("SqlServer:Clustered", true)
                .Annotation("SqlServer:DataCompression", DataCompressionType.Row)
                .Annotation("SqlServer:FillFactor", 95);

            migrationBuilder.CreateIndex(
                name: "IX_TaskDispatches_CallbackPending_CallbackAcceptedOnUtc",
                schema: "Runtime",
                table: "TaskDispatches",
                columns: new[] { "CallbackPending", "CallbackAcceptedOnUtc" },
                filter: "[CallbackPending] = 1")
                .Annotation("SqlServer:DataCompression", DataCompressionType.Row)
                .Annotation("SqlServer:FillFactor", 95);

            migrationBuilder.CreateIndex(
                name: "IX_TaskDispatches_CommandId",
                schema: "Runtime",
                table: "TaskDispatches",
                column: "CommandId")
                .Annotation("SqlServer:DataCompression", DataCompressionType.Row)
                .Annotation("SqlServer:FillFactor", 95);

            migrationBuilder.CreateIndex(
                name: "IX_TaskDispatches_PhysicalCancellationPending",
                schema: "Runtime",
                table: "TaskDispatches",
                column: "PhysicalCancellationPending",
                filter: "[PhysicalCancellationPending] = 1")
                .Annotation("SqlServer:DataCompression", DataCompressionType.Row)
                .Annotation("SqlServer:FillFactor", 95);

            migrationBuilder.CreateIndex(
                name: "IX_TaskDispatches_QueuePending_Id_ScheduledOnUtc",
                schema: "Runtime",
                table: "TaskDispatches",
                columns: new[] { "ScheduledOnUtc", "Id" },
                filter: "[DispatchStatus] = 'QueuePending'")
                .Annotation("SqlServer:DataCompression", DataCompressionType.Row)
                .Annotation("SqlServer:FillFactor", 95);

            migrationBuilder.CreateIndex(
                name: "IX_TaskDispatches_TaskExecutionAttemptId",
                schema: "Runtime",
                table: "TaskDispatches",
                column: "TaskExecutionAttemptId",
                unique: true)
                .Annotation("SqlServer:DataCompression", DataCompressionType.Row)
                .Annotation("SqlServer:FillFactor", 95);

            migrationBuilder.CreateIndex(
                name: "IX_SagaStatisticsObservations_InstanceId_Revision",
                schema: "Runtime",
                table: "SagaStatisticsObservations",
                columns: new[] { "InstanceId", "Revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SagaStatisticsObservations_Pending_InstanceId_Revision",
                schema: "Runtime",
                table: "SagaStatisticsObservations",
                columns: new[] { "InstanceId", "Revision" },
                filter: "[Processed] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_OrchestrationInstances_Idle_IdleExpiresOnUtc_Id",
                schema: "Runtime",
                table: "OrchestrationInstances",
                columns: new[] { "IdleExpiresOnUtc", "Id" },
                filter: "[Status] = 'Idle' AND [IdleExpiresOnUtc] IS NOT NULL")
                .Annotation("SqlServer:Include", new[] { "CorrelationId" });

            migrationBuilder.CreateIndex(
                name: "IX_OrchestrationInstances_Running_ExpiresOnUtc_Id",
                schema: "Runtime",
                table: "OrchestrationInstances",
                columns: new[] { "ExpiresOnUtc", "Id" },
                filter: "[Status] = 'Running' AND [ExpiresOnUtc] IS NOT NULL")
                .Annotation("SqlServer:Include", new[] { "CorrelationId" });

            migrationBuilder.CreateIndex(
                name: "IX_OrchestrationInstances_Running_StartedOnUtc_Id",
                schema: "Runtime",
                table: "OrchestrationInstances",
                columns: new[] { "StartedOnUtc", "Id" },
                descending: new bool[0],
                filter: "[Status] = 'Running'")
                .Annotation("SqlServer:Include", new[] { "CorrelationId" });

            migrationBuilder.CreateIndex(
                name: "IX_OrchestrationInstances_StartedOnUtc_Id",
                schema: "Runtime",
                table: "OrchestrationInstances",
                columns: new[] { "StartedOnUtc", "Id" },
                descending: new bool[0])
                .Annotation("SqlServer:Include", new[] { "CorrelationId" });

            migrationBuilder.CreateIndex(
                name: "IX_OrchestrationInstances_Status_ExpiresOnUtc_Id",
                schema: "Runtime",
                table: "OrchestrationInstances",
                columns: new[] { "Status", "ExpiresOnUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_OrchestrationInstances_Status_IdleExpiresOnUtc_Id",
                schema: "Runtime",
                table: "OrchestrationInstances",
                columns: new[] { "Status", "IdleExpiresOnUtc", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_TaskExecutions",
                schema: "Runtime",
                table: "TaskExecutions");

            migrationBuilder.DropIndex(
                name: "CIX_TaskExecutions_StorageBucket_OrchestrationInstanceId_Id",
                schema: "Runtime",
                table: "TaskExecutions");

            migrationBuilder.DropIndex(
                name: "IX_TaskExecutions_LegacyWaiting_WaitingSinceUtc_Id",
                schema: "Runtime",
                table: "TaskExecutions");

            migrationBuilder.DropIndex(
                name: "IX_TaskExecutions_Waiting_ResponseDeadlineUtc_Id",
                schema: "Runtime",
                table: "TaskExecutions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TaskExecutionAttempts",
                schema: "Runtime",
                table: "TaskExecutionAttempts");

            migrationBuilder.DropIndex(
                name: "CIX_TaskExecutionAttempts_StorageSequence",
                schema: "Runtime",
                table: "TaskExecutionAttempts");

            migrationBuilder.DropIndex(
                name: "IX_TaskExecutionAttempts_DispatchId",
                schema: "Runtime",
                table: "TaskExecutionAttempts");

            migrationBuilder.DropIndex(
                name: "IX_TaskExecutionAttempts_TaskExecutionId_AttemptNumber",
                schema: "Runtime",
                table: "TaskExecutionAttempts");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TaskDispatches",
                schema: "Runtime",
                table: "TaskDispatches");

            migrationBuilder.DropIndex(
                name: "CIX_TaskDispatches_StorageSequence",
                schema: "Runtime",
                table: "TaskDispatches");

            migrationBuilder.DropIndex(
                name: "IX_TaskDispatches_CallbackPending_CallbackAcceptedOnUtc",
                schema: "Runtime",
                table: "TaskDispatches");

            migrationBuilder.DropIndex(
                name: "IX_TaskDispatches_CommandId",
                schema: "Runtime",
                table: "TaskDispatches");

            migrationBuilder.DropIndex(
                name: "IX_TaskDispatches_PhysicalCancellationPending",
                schema: "Runtime",
                table: "TaskDispatches");

            migrationBuilder.DropIndex(
                name: "IX_TaskDispatches_QueuePending_Id_ScheduledOnUtc",
                schema: "Runtime",
                table: "TaskDispatches");

            migrationBuilder.DropIndex(
                name: "IX_TaskDispatches_TaskExecutionAttemptId",
                schema: "Runtime",
                table: "TaskDispatches");

            migrationBuilder.DropIndex(
                name: "IX_SagaStatisticsObservations_InstanceId_Revision",
                schema: "Runtime",
                table: "SagaStatisticsObservations");

            migrationBuilder.DropIndex(
                name: "IX_SagaStatisticsObservations_Pending_InstanceId_Revision",
                schema: "Runtime",
                table: "SagaStatisticsObservations");

            migrationBuilder.DropIndex(
                name: "IX_OrchestrationInstances_Idle_IdleExpiresOnUtc_Id",
                schema: "Runtime",
                table: "OrchestrationInstances");

            migrationBuilder.DropIndex(
                name: "IX_OrchestrationInstances_Running_ExpiresOnUtc_Id",
                schema: "Runtime",
                table: "OrchestrationInstances");

            migrationBuilder.DropIndex(
                name: "IX_OrchestrationInstances_Running_StartedOnUtc_Id",
                schema: "Runtime",
                table: "OrchestrationInstances");

            migrationBuilder.DropIndex(
                name: "IX_OrchestrationInstances_StartedOnUtc_Id",
                schema: "Runtime",
                table: "OrchestrationInstances");

            migrationBuilder.DropIndex(
                name: "IX_OrchestrationInstances_Status_ExpiresOnUtc_Id",
                schema: "Runtime",
                table: "OrchestrationInstances");

            migrationBuilder.DropIndex(
                name: "IX_OrchestrationInstances_Status_IdleExpiresOnUtc_Id",
                schema: "Runtime",
                table: "OrchestrationInstances");

            migrationBuilder.DropColumn(
                name: "StorageBucket",
                schema: "Runtime",
                table: "TaskExecutions");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TaskExecutions",
                schema: "Runtime",
                table: "TaskExecutions",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TaskExecutionAttempts",
                schema: "Runtime",
                table: "TaskExecutionAttempts",
                column: "Id")
                .Annotation("SqlServer:Clustered", false);

            migrationBuilder.AddPrimaryKey(
                name: "PK_TaskDispatches",
                schema: "Runtime",
                table: "TaskDispatches",
                column: "Id")
                .Annotation("SqlServer:Clustered", false);

            migrationBuilder.CreateIndex(
                name: "IX_TaskExecutions_LegacyWaiting_WaitingSinceUtc_Id",
                schema: "Runtime",
                table: "TaskExecutions",
                columns: new[] { "WaitingSinceUtc", "Id" },
                filter: "[Status] = 'WaitingResponse' AND [ResponseDeadlineUtc] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TaskExecutions_Waiting_ResponseDeadlineUtc_Id",
                schema: "Runtime",
                table: "TaskExecutions",
                columns: new[] { "ResponseDeadlineUtc", "Id" },
                filter: "[Status] = 'WaitingResponse' AND [ResponseDeadlineUtc] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "CIX_TaskExecutionAttempts_StorageSequence",
                schema: "Runtime",
                table: "TaskExecutionAttempts",
                column: "StorageSequence",
                unique: true)
                .Annotation("SqlServer:Clustered", true);

            migrationBuilder.CreateIndex(
                name: "IX_TaskExecutionAttempts_DispatchId",
                schema: "Runtime",
                table: "TaskExecutionAttempts",
                column: "DispatchId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskExecutionAttempts_TaskExecutionId_AttemptNumber",
                schema: "Runtime",
                table: "TaskExecutionAttempts",
                columns: new[] { "TaskExecutionId", "AttemptNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "CIX_TaskDispatches_StorageSequence",
                schema: "Runtime",
                table: "TaskDispatches",
                column: "StorageSequence",
                unique: true)
                .Annotation("SqlServer:Clustered", true);

            migrationBuilder.CreateIndex(
                name: "IX_TaskDispatches_CallbackPending_CallbackAcceptedOnUtc",
                schema: "Runtime",
                table: "TaskDispatches",
                columns: new[] { "CallbackPending", "CallbackAcceptedOnUtc" },
                filter: "[CallbackPending] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_TaskDispatches_CommandId",
                schema: "Runtime",
                table: "TaskDispatches",
                column: "CommandId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskDispatches_PhysicalCancellationPending",
                schema: "Runtime",
                table: "TaskDispatches",
                column: "PhysicalCancellationPending",
                filter: "[PhysicalCancellationPending] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_TaskDispatches_QueuePending_Id_ScheduledOnUtc",
                schema: "Runtime",
                table: "TaskDispatches",
                columns: new[] { "ScheduledOnUtc", "Id" },
                filter: "[DispatchStatus] = 'QueuePending'");

            migrationBuilder.CreateIndex(
                name: "IX_TaskDispatches_TaskExecutionAttemptId",
                schema: "Runtime",
                table: "TaskDispatches",
                column: "TaskExecutionAttemptId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SagaStatisticsObservations_Pending_InstanceId_Revision",
                schema: "Runtime",
                table: "SagaStatisticsObservations",
                columns: new[] { "InstanceId", "Revision" },
                unique: true,
                filter: "[Processed] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_OrchestrationInstances_Idle_IdleExpiresOnUtc_Id",
                schema: "Runtime",
                table: "OrchestrationInstances",
                columns: new[] { "IdleExpiresOnUtc", "Id" },
                filter: "[Status] = 'Idle' AND [IdleExpiresOnUtc] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OrchestrationInstances_Running_ExpiresOnUtc_Id",
                schema: "Runtime",
                table: "OrchestrationInstances",
                columns: new[] { "ExpiresOnUtc", "Id" },
                filter: "[Status] = 'Running' AND [ExpiresOnUtc] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OrchestrationInstances_Running_StartedOnUtc_Id",
                schema: "Runtime",
                table: "OrchestrationInstances",
                columns: new[] { "StartedOnUtc", "Id" },
                filter: "[Status] = 'Running'");
        }
    }
}
