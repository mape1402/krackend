using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Krackend.Sagas.Orchestrations.RuntimeHost.Sample.Migrations.RuntimeStorage
{
    /// <inheritdoc />
    public partial class InitialRuntimeStorageSqlServer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "Runtime");

            migrationBuilder.CreateTable(
                name: "Artifacts",
                schema: "Runtime",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", nullable: false),
                    EnvironmentKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    OrchestrationDefinitionKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    SourceOrchestrationVersionId = table.Column<byte[]>(type: "binary(16)", nullable: false),
                    Version = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ArtifactChecksum = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ArtifactPayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    LoadedToCache = table.Column<bool>(type: "bit", nullable: false),
                    DeployedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ActivatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RetiredOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SupersededByArtifactId = table.Column<byte[]>(type: "binary(16)", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Artifacts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CompensationExecutions",
                schema: "Runtime",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", nullable: false),
                    OrchestrationInstanceId = table.Column<byte[]>(type: "binary(16)", nullable: false),
                    SourceTaskExecutionId = table.Column<byte[]>(type: "binary(16)", nullable: false),
                    CompensationTaskKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    StartedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FailedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RequestPayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ResponsePayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    MetadataJson = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompensationExecutions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EnvironmentVariables",
                schema: "Runtime",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", nullable: false),
                    EnvironmentKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    VariableKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ValueType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ValueJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsSensitive = table.Column<bool>(type: "bit", nullable: false),
                    IsResolved = table.Column<bool>(type: "bit", nullable: false),
                    LastValidatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnvironmentVariables", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ExecutionTransitions",
                schema: "Runtime",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", nullable: false),
                    OrchestrationInstanceId = table.Column<byte[]>(type: "binary(16)", nullable: false),
                    StageExecutionId = table.Column<byte[]>(type: "binary(16)", nullable: true),
                    TaskExecutionId = table.Column<byte[]>(type: "binary(16)", nullable: true),
                    TaskExecutionAttemptId = table.Column<byte[]>(type: "binary(16)", nullable: true),
                    TransitionType = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    FromStatus = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ToStatus = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    OccurredOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Message = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ProducedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExecutionTransitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InstanceVariables",
                schema: "Runtime",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", nullable: false),
                    OrchestrationInstanceId = table.Column<byte[]>(type: "binary(16)", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Scope = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ValueType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ValueJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsSensitive = table.Column<bool>(type: "bit", nullable: false),
                    SourceType = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    SourceReference = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastUpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InstanceVariables", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OrchestrationInstances",
                schema: "Runtime",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", nullable: false),
                    EnvironmentKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    OrchestrationDefinitionKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    RuntimeOrchestrationArtifactId = table.Column<byte[]>(type: "binary(16)", nullable: false),
                    TriggerIntakeId = table.Column<byte[]>(type: "binary(16)", nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ExecutionKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CurrentStageKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CurrentTaskKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CurrentParallelGroupKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    StartedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastUpdatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    WaitingSinceUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FailedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    StoppedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompensationStartedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompensatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FinalOutcome = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    ErrorSummary = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    RetryCount = table.Column<int>(type: "int", nullable: false),
                    ActiveLeaseId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ActiveLeaseExpiresOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SnapshotPayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MetadataJson = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrchestrationInstances", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StageExecutions",
                schema: "Runtime",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", nullable: false),
                    OrchestrationInstanceId = table.Column<byte[]>(type: "binary(16)", nullable: false),
                    StageKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    WasSkipped = table.Column<bool>(type: "bit", nullable: false),
                    SkipReason = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    ExecutionConditionResult = table.Column<bool>(type: "bit", nullable: true),
                    StartedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FailedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ErrorSummary = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    ParallelGroupCount = table.Column<int>(type: "int", nullable: false),
                    MetadataJson = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StageExecutions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TaskDispatches",
                schema: "Runtime",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", nullable: false),
                    TaskExecutionAttemptId = table.Column<byte[]>(type: "binary(16)", nullable: false),
                    DispatchType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Destination = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    RequestPayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DispatchStatus = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CommandId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    SentOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AcknowledgedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FailedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FailureReason = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    MetadataJson = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskDispatches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TaskExecutionAttempts",
                schema: "Runtime",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", nullable: false),
                    TaskExecutionId = table.Column<byte[]>(type: "binary(16)", nullable: false),
                    AttemptNumber = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    StartedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    WaitingSinceUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FailedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TimedOutOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RequestPayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ResponsePayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ErrorCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    DispatchId = table.Column<byte[]>(type: "binary(16)", nullable: true),
                    MetadataJson = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskExecutionAttempts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TaskExecutions",
                schema: "Runtime",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", nullable: false),
                    OrchestrationInstanceId = table.Column<byte[]>(type: "binary(16)", nullable: false),
                    StageExecutionId = table.Column<byte[]>(type: "binary(16)", nullable: false),
                    TaskKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    TaskKind = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ExecutionMode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ParallelGroupId = table.Column<byte[]>(type: "binary(16)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    WasSkipped = table.Column<bool>(type: "bit", nullable: false),
                    SkipReason = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    ExecutionConditionResult = table.Column<bool>(type: "bit", nullable: true),
                    OnErrorPolicy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    AwaitResponse = table.Column<bool>(type: "bit", nullable: false),
                    StartedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    WaitingSinceUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FailedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TimedOutOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastAttemptNumber = table.Column<int>(type: "int", nullable: false),
                    OutputVariablesPayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    MetadataJson = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskExecutions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TriggerIntakeAttempts",
                schema: "Runtime",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", nullable: false),
                    TriggerIntakeId = table.Column<byte[]>(type: "binary(16)", nullable: false),
                    AttemptNumber = table.Column<int>(type: "int", nullable: false),
                    ActionType = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Outcome = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    StartedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FinishedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ErrorCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    MetadataJson = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TriggerIntakeAttempts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TriggerIntakes",
                schema: "Runtime",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", nullable: false),
                    TriggerType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    TriggerKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    EnvironmentKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    SourceMessageId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    SourceRequestId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    RawPayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NormalizedPayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    PersistenceLevel = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    BufferLocation = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    ResolvedArtifactId = table.Column<byte[]>(type: "binary(16)", nullable: true),
                    PromotedInstanceId = table.Column<byte[]>(type: "binary(16)", nullable: true),
                    ReceivedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PromotedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExpiresOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    FailureReason = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TriggerIntakes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Artifacts_EnvironmentKey_OrchestrationDefinitionKey_IsActive",
                schema: "Runtime",
                table: "Artifacts",
                columns: new[] { "EnvironmentKey", "OrchestrationDefinitionKey", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_Artifacts_EnvironmentKey_OrchestrationDefinitionKey_Version",
                schema: "Runtime",
                table: "Artifacts",
                columns: new[] { "EnvironmentKey", "OrchestrationDefinitionKey", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CompensationExecutions_OrchestrationInstanceId_SourceTaskExecutionId",
                schema: "Runtime",
                table: "CompensationExecutions",
                columns: new[] { "OrchestrationInstanceId", "SourceTaskExecutionId" });

            migrationBuilder.CreateIndex(
                name: "IX_EnvironmentVariables_EnvironmentKey_VariableKey",
                schema: "Runtime",
                table: "EnvironmentVariables",
                columns: new[] { "EnvironmentKey", "VariableKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExecutionTransitions_OrchestrationInstanceId_OccurredOnUtc",
                schema: "Runtime",
                table: "ExecutionTransitions",
                columns: new[] { "OrchestrationInstanceId", "OccurredOnUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_InstanceVariables_OrchestrationInstanceId_Key",
                schema: "Runtime",
                table: "InstanceVariables",
                columns: new[] { "OrchestrationInstanceId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrchestrationInstances_CorrelationId",
                schema: "Runtime",
                table: "OrchestrationInstances",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_OrchestrationInstances_EnvironmentKey_Status",
                schema: "Runtime",
                table: "OrchestrationInstances",
                columns: new[] { "EnvironmentKey", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_OrchestrationInstances_ExecutionKey",
                schema: "Runtime",
                table: "OrchestrationInstances",
                column: "ExecutionKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StageExecutions_OrchestrationInstanceId_StageKey",
                schema: "Runtime",
                table: "StageExecutions",
                columns: new[] { "OrchestrationInstanceId", "StageKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TaskDispatches_CommandId",
                schema: "Runtime",
                table: "TaskDispatches",
                column: "CommandId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskDispatches_CorrelationId",
                schema: "Runtime",
                table: "TaskDispatches",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskExecutionAttempts_TaskExecutionId_AttemptNumber",
                schema: "Runtime",
                table: "TaskExecutionAttempts",
                columns: new[] { "TaskExecutionId", "AttemptNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TaskExecutions_CorrelationId",
                schema: "Runtime",
                table: "TaskExecutions",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskExecutions_OrchestrationInstanceId_TaskKey",
                schema: "Runtime",
                table: "TaskExecutions",
                columns: new[] { "OrchestrationInstanceId", "TaskKey" });

            migrationBuilder.CreateIndex(
                name: "IX_TriggerIntakeAttempts_TriggerIntakeId_AttemptNumber",
                schema: "Runtime",
                table: "TriggerIntakeAttempts",
                columns: new[] { "TriggerIntakeId", "AttemptNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_TriggerIntakes_EnvironmentKey_IdempotencyKey",
                schema: "Runtime",
                table: "TriggerIntakes",
                columns: new[] { "EnvironmentKey", "IdempotencyKey" });

            migrationBuilder.CreateIndex(
                name: "IX_TriggerIntakes_EnvironmentKey_TriggerKey_Status",
                schema: "Runtime",
                table: "TriggerIntakes",
                columns: new[] { "EnvironmentKey", "TriggerKey", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Artifacts",
                schema: "Runtime");

            migrationBuilder.DropTable(
                name: "CompensationExecutions",
                schema: "Runtime");

            migrationBuilder.DropTable(
                name: "EnvironmentVariables",
                schema: "Runtime");

            migrationBuilder.DropTable(
                name: "ExecutionTransitions",
                schema: "Runtime");

            migrationBuilder.DropTable(
                name: "InstanceVariables",
                schema: "Runtime");

            migrationBuilder.DropTable(
                name: "OrchestrationInstances",
                schema: "Runtime");

            migrationBuilder.DropTable(
                name: "StageExecutions",
                schema: "Runtime");

            migrationBuilder.DropTable(
                name: "TaskDispatches",
                schema: "Runtime");

            migrationBuilder.DropTable(
                name: "TaskExecutionAttempts",
                schema: "Runtime");

            migrationBuilder.DropTable(
                name: "TaskExecutions",
                schema: "Runtime");

            migrationBuilder.DropTable(
                name: "TriggerIntakeAttempts",
                schema: "Runtime");

            migrationBuilder.DropTable(
                name: "TriggerIntakes",
                schema: "Runtime");
        }
    }
}
