using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Krackend.Sagas.Orchestrations.RuntimeHost.Sample.Migrations.RuntimeStorage
{
    /// <inheritdoc />
    public partial class AlignRuntimeConcurrencyModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TaskExecutions_Status",
                schema: "Runtime",
                table: "TaskExecutions");

            migrationBuilder.DropIndex(
                name: "IX_TaskExecutions_Status_ResponseDeadlineUtc",
                schema: "Runtime",
                table: "TaskExecutions");

            migrationBuilder.DropIndex(
                name: "IX_TaskExecutions_Status_TimeoutActionId",
                schema: "Runtime",
                table: "TaskExecutions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TaskExecutionAttempts",
                schema: "Runtime",
                table: "TaskExecutionAttempts");

            migrationBuilder.DropIndex(
                name: "IX_TaskExecutionAttempts_Status",
                schema: "Runtime",
                table: "TaskExecutionAttempts");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TaskDispatches",
                schema: "Runtime",
                table: "TaskDispatches");

            migrationBuilder.DropIndex(
                name: "IX_TaskDispatches_CallbackPending_CallbackAcceptedOnUtc",
                schema: "Runtime",
                table: "TaskDispatches");

            migrationBuilder.DropIndex(
                name: "IX_TaskDispatches_DispatchStatus",
                schema: "Runtime",
                table: "TaskDispatches");

            migrationBuilder.DropIndex(
                name: "IX_TaskDispatches_PhysicalCancellationPending",
                schema: "Runtime",
                table: "TaskDispatches");

            migrationBuilder.DropIndex(
                name: "IX_SagaStatisticsObservations_InstanceId_Revision",
                schema: "Runtime",
                table: "SagaStatisticsObservations");

            migrationBuilder.DropIndex(
                name: "IX_SagaStatisticsObservations_Processed_InstanceId_Revision",
                schema: "Runtime",
                table: "SagaStatisticsObservations");

            migrationBuilder.DropIndex(
                name: "IX_OrchestrationInstances_DispatchCancellationPending_LastUpdatedOnUtc",
                schema: "Runtime",
                table: "OrchestrationInstances");

            migrationBuilder.DropIndex(
                name: "IX_OrchestrationInstances_LastUpdatedOnUtc",
                schema: "Runtime",
                table: "OrchestrationInstances");

            migrationBuilder.DropIndex(
                name: "IX_OrchestrationInstances_SagaId",
                schema: "Runtime",
                table: "OrchestrationInstances");

            migrationBuilder.DropIndex(
                name: "IX_OrchestrationInstances_StartedOnUtc_Status",
                schema: "Runtime",
                table: "OrchestrationInstances");

            migrationBuilder.DropIndex(
                name: "IX_OrchestrationInstances_Status_ExpiresOnUtc_Id",
                schema: "Runtime",
                table: "OrchestrationInstances");

            migrationBuilder.DropIndex(
                name: "IX_OrchestrationInstances_Status_IdleExpiresOnUtc",
                schema: "Runtime",
                table: "OrchestrationInstances");

            migrationBuilder.DropIndex(
                name: "IX_OrchestrationInstances_Status_LastUpdatedOnUtc",
                schema: "Runtime",
                table: "OrchestrationInstances");

            migrationBuilder.CreateSequence(
                name: "RuntimeStorageSequence",
                schema: "Runtime");

            migrationBuilder.AddColumn<long>(
                name: "StorageSequence",
                schema: "Runtime",
                table: "TaskExecutionAttempts",
                type: "bigint",
                nullable: false,
                defaultValueSql: "NEXT VALUE FOR [Runtime].[RuntimeStorageSequence]");

            migrationBuilder.AddColumn<long>(
                name: "StorageSequence",
                schema: "Runtime",
                table: "TaskDispatches",
                type: "bigint",
                nullable: false,
                defaultValueSql: "NEXT VALUE FOR [Runtime].[RuntimeStorageSequence]");

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
                name: "IX_TaskExecutions_OrchestrationInstanceId",
                schema: "Runtime",
                table: "TaskExecutions",
                column: "OrchestrationInstanceId");

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
                name: "IX_SagaStatisticsObservations_Pending_InstanceId_Revision",
                schema: "Runtime",
                table: "SagaStatisticsObservations",
                columns: new[] { "InstanceId", "Revision" },
                unique: true,
                filter: "[Processed] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_OrchestrationInstances_DispatchCancellationPending_Id",
                schema: "Runtime",
                table: "OrchestrationInstances",
                column: "Id",
                filter: "[DispatchCancellationPending] = 1");

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

            migrationBuilder.CreateIndex(
                name: "IX_DurableAction_CorrelationId",
                schema: "Runtime",
                table: "DurableAction",
                column: "CorrelationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TaskExecutions_LegacyWaiting_WaitingSinceUtc_Id",
                schema: "Runtime",
                table: "TaskExecutions");

            migrationBuilder.DropIndex(
                name: "IX_TaskExecutions_OrchestrationInstanceId",
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
                name: "IX_TaskDispatches_PhysicalCancellationPending",
                schema: "Runtime",
                table: "TaskDispatches");

            migrationBuilder.DropIndex(
                name: "IX_TaskDispatches_QueuePending_Id_ScheduledOnUtc",
                schema: "Runtime",
                table: "TaskDispatches");

            migrationBuilder.DropIndex(
                name: "IX_SagaStatisticsObservations_Pending_InstanceId_Revision",
                schema: "Runtime",
                table: "SagaStatisticsObservations");

            migrationBuilder.DropIndex(
                name: "IX_OrchestrationInstances_DispatchCancellationPending_Id",
                schema: "Runtime",
                table: "OrchestrationInstances");

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
                name: "IX_DurableAction_CorrelationId",
                schema: "Runtime",
                table: "DurableAction");

            migrationBuilder.DropColumn(
                name: "StorageSequence",
                schema: "Runtime",
                table: "TaskExecutionAttempts");

            migrationBuilder.DropColumn(
                name: "StorageSequence",
                schema: "Runtime",
                table: "TaskDispatches");

            migrationBuilder.DropSequence(
                name: "RuntimeStorageSequence",
                schema: "Runtime");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TaskExecutionAttempts",
                schema: "Runtime",
                table: "TaskExecutionAttempts",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TaskDispatches",
                schema: "Runtime",
                table: "TaskDispatches",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_TaskExecutions_Status",
                schema: "Runtime",
                table: "TaskExecutions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_TaskExecutions_Status_ResponseDeadlineUtc",
                schema: "Runtime",
                table: "TaskExecutions",
                columns: new[] { "Status", "ResponseDeadlineUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_TaskExecutions_Status_TimeoutActionId",
                schema: "Runtime",
                table: "TaskExecutions",
                columns: new[] { "Status", "TimeoutActionId" });

            migrationBuilder.CreateIndex(
                name: "IX_TaskExecutionAttempts_Status",
                schema: "Runtime",
                table: "TaskExecutionAttempts",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_TaskDispatches_CallbackPending_CallbackAcceptedOnUtc",
                schema: "Runtime",
                table: "TaskDispatches",
                columns: new[] { "CallbackPending", "CallbackAcceptedOnUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_TaskDispatches_DispatchStatus",
                schema: "Runtime",
                table: "TaskDispatches",
                column: "DispatchStatus");

            migrationBuilder.CreateIndex(
                name: "IX_TaskDispatches_PhysicalCancellationPending",
                schema: "Runtime",
                table: "TaskDispatches",
                column: "PhysicalCancellationPending");

            migrationBuilder.CreateIndex(
                name: "IX_SagaStatisticsObservations_InstanceId_Revision",
                schema: "Runtime",
                table: "SagaStatisticsObservations",
                columns: new[] { "InstanceId", "Revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SagaStatisticsObservations_Processed_InstanceId_Revision",
                schema: "Runtime",
                table: "SagaStatisticsObservations",
                columns: new[] { "Processed", "InstanceId", "Revision" });

            migrationBuilder.CreateIndex(
                name: "IX_OrchestrationInstances_DispatchCancellationPending_LastUpdatedOnUtc",
                schema: "Runtime",
                table: "OrchestrationInstances",
                columns: new[] { "DispatchCancellationPending", "LastUpdatedOnUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_OrchestrationInstances_LastUpdatedOnUtc",
                schema: "Runtime",
                table: "OrchestrationInstances",
                column: "LastUpdatedOnUtc");

            migrationBuilder.CreateIndex(
                name: "IX_OrchestrationInstances_SagaId",
                schema: "Runtime",
                table: "OrchestrationInstances",
                column: "SagaId");

            migrationBuilder.CreateIndex(
                name: "IX_OrchestrationInstances_StartedOnUtc_Status",
                schema: "Runtime",
                table: "OrchestrationInstances",
                columns: new[] { "StartedOnUtc", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_OrchestrationInstances_Status_ExpiresOnUtc_Id",
                schema: "Runtime",
                table: "OrchestrationInstances",
                columns: new[] { "Status", "ExpiresOnUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_OrchestrationInstances_Status_IdleExpiresOnUtc",
                schema: "Runtime",
                table: "OrchestrationInstances",
                columns: new[] { "Status", "IdleExpiresOnUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_OrchestrationInstances_Status_LastUpdatedOnUtc",
                schema: "Runtime",
                table: "OrchestrationInstances",
                columns: new[] { "Status", "LastUpdatedOnUtc" });
        }
    }
}
