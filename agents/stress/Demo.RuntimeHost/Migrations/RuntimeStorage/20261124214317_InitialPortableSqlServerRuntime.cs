using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Demo.RuntimeHost.Migrations.RuntimeStorage
{
    /// <inheritdoc />
    public partial class InitialPortableSqlServerRuntime : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "Runtime");

            migrationBuilder.CreateSequence(
                name: "RuntimeStorageSequence",
                schema: "Runtime");

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
                    RequestPayload = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ResponsePayload = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Metadata = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompensationExecutions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DataProtectionKeys",
                schema: "Runtime",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FriendlyName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Xml = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataProtectionKeys", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DurableAction",
                schema: "Runtime",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(26)", maxLength: 26, nullable: false),
                    ConcurrencyToken = table.Column<string>(type: "nvarchar(26)", maxLength: 26, nullable: false),
                    Key = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Lane = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Payload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PayloadType = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Metadata = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    DeduplicationKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    LastError = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LockedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    StartedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    NextAttemptOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    PendingReason = table.Column<int>(type: "int", nullable: false),
                    DeferralReasonCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    CompletedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CancelledOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    TerminalOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DurableAction", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EnvironmentVariableValues",
                schema: "Runtime",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", nullable: false),
                    VariableKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ValueType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsSensitive = table.Column<bool>(type: "bit", nullable: false),
                    IsResolved = table.Column<bool>(type: "bit", nullable: false),
                    LastValidatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnvironmentVariableValues", x => x.Id);
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
                    TransitionType = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    FromStatus = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ToStatus = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    OccurredOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Message = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Payload = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsSensitive = table.Column<bool>(type: "bit", nullable: false),
                    SourceType = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    SourceReference = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastUpdatedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InstanceVariables", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MuleReplay",
                schema: "Runtime",
                columns: table => new
                {
                    RequestId = table.Column<string>(type: "nvarchar(26)", maxLength: 26, nullable: false),
                    ConcurrencyToken = table.Column<string>(type: "nvarchar(26)", maxLength: 26, nullable: false),
                    SourceActionId = table.Column<string>(type: "nvarchar(26)", maxLength: 26, nullable: false),
                    ReplayRootActionId = table.Column<string>(type: "nvarchar(26)", maxLength: 26, nullable: false),
                    ReplayActionId = table.Column<string>(type: "nvarchar(26)", maxLength: 26, nullable: false),
                    ReplaySequence = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<string>(type: "nvarchar(26)", maxLength: 26, nullable: false),
                    ActionKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Lane = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    OriginalDeduplicationKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RequestedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Metadata = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RequestedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ScheduleOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    NextAttemptOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastError = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LockedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LockToken = table.Column<string>(type: "nvarchar(26)", maxLength: 26, nullable: false),
                    MaterializedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CancelledOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MuleReplay", x => x.RequestId);
                });

            migrationBuilder.CreateTable(
                name: "OrchestrationInstances",
                schema: "Runtime",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", nullable: false),
                    OrchestrationDefinitionKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    RuntimeOrchestrationArtifactId = table.Column<byte[]>(type: "binary(16)", nullable: false),
                    MaxInstanceDurationSeconds = table.Column<int>(type: "int", nullable: true),
                    IdleDurationSeconds = table.Column<int>(type: "int", nullable: true),
                    ExpiresOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    OrchestrationVersion = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    TotalTaskCount = table.Column<int>(type: "int", nullable: true),
                    CompletedTaskCount = table.Column<int>(type: "int", nullable: true),
                    TriggerIntakeId = table.Column<byte[]>(type: "binary(16)", nullable: false),
                    StartIdempotencyKey = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    SagaId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ExecutionKey = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CurrentStageKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CurrentTaskKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    LastExecutedStageKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    LastExecutedTaskKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CurrentParallelGroupKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    StartedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastUpdatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StatisticsRevision = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                    DispatchCancellationPending = table.Column<bool>(type: "bit", nullable: false),
                    WaitingSinceUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IdleSinceUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IdleExpiresOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FailedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    StoppedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompensationStartedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompensatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FinalOutcome = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ErrorSummary = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    RetryCount = table.Column<int>(type: "int", nullable: false),
                    ActiveLeaseId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ActiveLeaseExpiresOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SnapshotPayload = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Metadata = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrchestrationInstances", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RuntimeArtifactDeploymentHistory",
                schema: "Runtime",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", nullable: false),
                    RuntimeOrchestrationArtifactId = table.Column<byte[]>(type: "binary(16)", nullable: false),
                    OrchestrationDefinitionKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    OrchestrationVersion = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ControlPlaneArtifactId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ReleaseId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ArtifactType = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ReleaseStrategy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    LifecycleStatus = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    RuntimeStatus = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    AppliedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RuntimeArtifactDeploymentHistory", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RuntimeDesignNodes",
                schema: "Runtime",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    EndpointBaseUri = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    RemoteRuntimeNodeId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    DistributionMode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    AccessTokenTtlSeconds = table.Column<int>(type: "int", nullable: false),
                    TokenRefreshSkewSeconds = table.Column<int>(type: "int", nullable: false),
                    TokenValidationCacheTtlSeconds = table.Column<int>(type: "int", nullable: false),
                    InboundClientId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    InboundKeyId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    InboundSecretHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    InboundAllowedScopes = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    InboundCredentialStatus = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    InboundCredentialCreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    InboundCredentialRotatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    InboundCredentialRevokedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    InboundLastTokenIssuedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    InboundLastTokenFailedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    InboundLastFailureReason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    OutboundClientId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    OutboundKeyId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ProtectedOutboundSecret = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OutboundRequestedScopes = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    OutboundCredentialStatus = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    OutboundCredentialImportedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    OutboundLastTokenReceivedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false, defaultValue: "Pending"),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RuntimeDesignNodes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RuntimeExtensionPackages",
                schema: "Runtime",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", nullable: false),
                    BundleId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ExtensionKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Version = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Sha256 = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Manifest = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ActivatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    StatusReason = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RuntimeExtensionPackages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RuntimeIngressConfigurations",
                schema: "Runtime",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", nullable: false),
                    RuntimeOrchestrationArtifactId = table.Column<byte[]>(type: "binary(16)", nullable: false),
                    ConfigurationKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    IngressKind = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    MessageDisposition = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    IngressTransport = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SettingsPayload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RouteTopic = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    RouteVersion = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DeactivatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RuntimeIngressConfigurations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RuntimeOrchestrationArtifacts",
                schema: "Runtime",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", nullable: false),
                    OrchestrationDefinitionKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ArtifactType = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    SourceOrchestrationVersionId = table.Column<byte[]>(type: "binary(16)", nullable: false),
                    Version = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ArtifactChecksum = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ArtifactPayload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false, defaultValue: "Pending"),
                    LifecycleStatus = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false, defaultValue: "Active"),
                    LastReleaseId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    LastControlPlaneArtifactId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ReleaseStrategy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    IngressGeneration = table.Column<long>(type: "bigint", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    LoadedToCache = table.Column<bool>(type: "bit", nullable: false),
                    DeployedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ActivatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ProjectionStartedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ProjectionCompletedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ProjectionFailedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ProjectionError = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    RetiredOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SupersededByArtifactId = table.Column<byte[]>(type: "binary(16)", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RuntimeOrchestrationArtifacts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RuntimeTriggerClaims",
                schema: "Runtime",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    RuntimeOrchestrationArtifactId = table.Column<byte[]>(type: "binary(16)", nullable: false),
                    UpdatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RuntimeTriggerClaims", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SagaFailureBackfillCheckpoints",
                schema: "Runtime",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    BeforeUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CursorUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CursorOffset = table.Column<int>(type: "int", nullable: false),
                    Completed = table.Column<bool>(type: "bit", nullable: false),
                    ConcurrencyToken = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SagaFailureBackfillCheckpoints", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SagaFailureEvents",
                schema: "Runtime",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", nullable: false),
                    OrchestrationInstanceId = table.Column<byte[]>(type: "binary(16)", nullable: false),
                    OccurredOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TransitionType = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    OrchestrationKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    OrchestrationVersion = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ScopeProjectionVersion = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SagaFailureEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SagaStatisticsBackfillCheckpoints",
                schema: "Runtime",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CompletedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SagaStatisticsBackfillCheckpoints", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SagaStatisticsObservations",
                schema: "Runtime",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    InstanceId = table.Column<byte[]>(type: "binary(16)", nullable: false),
                    ArtifactId = table.Column<byte[]>(type: "binary(16)", nullable: false),
                    OrchestrationKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    StageKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    StartedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    OccurredOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Processed = table.Column<bool>(type: "bit", nullable: false),
                    StatusChanged = table.Column<bool>(type: "bit", nullable: false),
                    StatusFirstReached = table.Column<bool>(type: "bit", nullable: false),
                    IsBackfill = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SagaStatisticsObservations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SagaStatisticsProjections",
                schema: "Runtime",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(26)", maxLength: 26, nullable: false),
                    ArtifactId = table.Column<byte[]>(type: "binary(16)", nullable: false),
                    OrchestrationKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    StartedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastObservedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReachedStatuses = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SagaStatisticsProjections", x => x.Id);
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
                    SkipReason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ExecutionConditionResult = table.Column<bool>(type: "bit", nullable: true),
                    StartedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FailedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ErrorSummary = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    ParallelGroupCount = table.Column<int>(type: "int", nullable: false),
                    Metadata = table.Column<string>(type: "nvarchar(max)", nullable: true)
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
                    RequestPayload = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DispatchStatus = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CommandId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    SentOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CallbackReceiptId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    CallbackAcceptedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PendingCallbackPayload = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CallbackPending = table.Column<bool>(type: "bit", nullable: false),
                    ScheduledOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AcknowledgedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FailedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FailureReason = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    PhysicalCancellationPending = table.Column<bool>(type: "bit", nullable: false),
                    PhysicalCancellationAttemptCount = table.Column<int>(type: "int", nullable: false),
                    PhysicalCancellationLastAttemptOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PhysicalCancellationCompletedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Metadata = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StorageSequence = table.Column<long>(type: "bigint", nullable: false, defaultValueSql: "NEXT VALUE FOR [Runtime].[RuntimeStorageSequence]")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskDispatches", x => x.Id)
                        .Annotation("SqlServer:Clustered", false)
                        .Annotation("SqlServer:FillFactor", 95);
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
                    RequestPayload = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ResponsePayload = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ErrorCode = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    DispatchId = table.Column<byte[]>(type: "binary(16)", nullable: true),
                    Metadata = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StorageSequence = table.Column<long>(type: "bigint", nullable: false, defaultValueSql: "NEXT VALUE FOR [Runtime].[RuntimeStorageSequence]")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskExecutionAttempts", x => x.Id)
                        .Annotation("SqlServer:Clustered", false)
                        .Annotation("SqlServer:FillFactor", 95);
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
                    SkipReason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ExecutionConditionResult = table.Column<bool>(type: "bit", nullable: true),
                    OnErrorPolicy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    AwaitResponse = table.Column<bool>(type: "bit", nullable: false),
                    StartedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    WaitingSinceUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResponseDeadlineUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TimeoutActionId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    TimeoutActionLane = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    CompletedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FailedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TimedOutOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastAttemptNumber = table.Column<int>(type: "int", nullable: false),
                    OutputVariablesPayload = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Metadata = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StorageBucket = table.Column<byte>(type: "tinyint", nullable: false, computedColumnSql: "CONVERT(tinyint, CONVERT(int, SUBSTRING([OrchestrationInstanceId], 16, 1)) & 63)", stored: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskExecutions", x => x.Id)
                        .Annotation("SqlServer:Clustered", false)
                        .Annotation("SqlServer:FillFactor", 95);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CompensationExecutions_Instance_SourceTask_CompensationTask",
                schema: "Runtime",
                table: "CompensationExecutions",
                columns: new[] { "OrchestrationInstanceId", "SourceTaskExecutionId", "CompensationTaskKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CompensationExecutions_OrchestrationInstanceId",
                schema: "Runtime",
                table: "CompensationExecutions",
                column: "OrchestrationInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_DurableAction_CorrelationId",
                schema: "Runtime",
                table: "DurableAction",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_DurableAction_Key_DeduplicationKey",
                schema: "Runtime",
                table: "DurableAction",
                columns: new[] { "Key", "DeduplicationKey" });

            migrationBuilder.CreateIndex(
                name: "IX_DurableAction_Lane_Status_NextAttemptOnUtc_CreatedOnUtc",
                schema: "Runtime",
                table: "DurableAction",
                columns: new[] { "Lane", "Status", "NextAttemptOnUtc", "CreatedOnUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_DurableAction_Status_CompletedOnUtc",
                schema: "Runtime",
                table: "DurableAction",
                columns: new[] { "Status", "CompletedOnUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_DurableAction_Status_LockedOnUtc",
                schema: "Runtime",
                table: "DurableAction",
                columns: new[] { "Status", "LockedOnUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_DurableAction_Status_PendingReason_NextAttemptOnUtc",
                schema: "Runtime",
                table: "DurableAction",
                columns: new[] { "Status", "PendingReason", "NextAttemptOnUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_EnvironmentVariableValues_VariableKey",
                schema: "Runtime",
                table: "EnvironmentVariableValues",
                column: "VariableKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExecutionTransitions_OccurredOnUtc",
                schema: "Runtime",
                table: "ExecutionTransitions",
                column: "OccurredOnUtc");

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
                name: "IX_MuleReplay_ReplayActionId",
                schema: "Runtime",
                table: "MuleReplay",
                column: "ReplayActionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MuleReplay_SourceActionId_BranchId",
                schema: "Runtime",
                table: "MuleReplay",
                columns: new[] { "SourceActionId", "BranchId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MuleReplay_Status_LockedOnUtc",
                schema: "Runtime",
                table: "MuleReplay",
                columns: new[] { "Status", "LockedOnUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_MuleReplay_Status_ScheduleOnUtc_NextAttemptOnUtc",
                schema: "Runtime",
                table: "MuleReplay",
                columns: new[] { "Status", "ScheduleOnUtc", "NextAttemptOnUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_OrchestrationInstances_CorrelationId",
                schema: "Runtime",
                table: "OrchestrationInstances",
                column: "CorrelationId");

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
                name: "IX_OrchestrationInstances_StartIdempotencyKey",
                schema: "Runtime",
                table: "OrchestrationInstances",
                column: "StartIdempotencyKey",
                unique: true,
                filter: "[StartIdempotencyKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OrchestrationInstances_StatisticsRevision",
                schema: "Runtime",
                table: "OrchestrationInstances",
                column: "StatisticsRevision",
                filter: "[StatisticsRevision] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_RuntimeArtifactDeploymentHistory_ControlPlaneArtifactId",
                schema: "Runtime",
                table: "RuntimeArtifactDeploymentHistory",
                column: "ControlPlaneArtifactId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RuntimeArtifactDeploymentHistory_OrchestrationDefinitionKey_AppliedOnUtc",
                schema: "Runtime",
                table: "RuntimeArtifactDeploymentHistory",
                columns: new[] { "OrchestrationDefinitionKey", "AppliedOnUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_RuntimeArtifactDeploymentHistory_ReleaseId",
                schema: "Runtime",
                table: "RuntimeArtifactDeploymentHistory",
                column: "ReleaseId");

            migrationBuilder.CreateIndex(
                name: "IX_RuntimeDesignNodes_InboundClientId",
                schema: "Runtime",
                table: "RuntimeDesignNodes",
                column: "InboundClientId",
                unique: true,
                filter: "[InboundClientId] IS NOT NULL AND [InboundClientId] <> ''");

            migrationBuilder.CreateIndex(
                name: "IX_RuntimeDesignNodes_IsEnabled",
                schema: "Runtime",
                table: "RuntimeDesignNodes",
                column: "IsEnabled");

            migrationBuilder.CreateIndex(
                name: "IX_RuntimeDesignNodes_Key",
                schema: "Runtime",
                table: "RuntimeDesignNodes",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RuntimeDesignNodes_Status",
                schema: "Runtime",
                table: "RuntimeDesignNodes",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_RuntimeExtensionPackages_BundleId_ExtensionKey_Version",
                schema: "Runtime",
                table: "RuntimeExtensionPackages",
                columns: new[] { "BundleId", "ExtensionKey", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RuntimeExtensionPackages_ExtensionKey_Version_Status",
                schema: "Runtime",
                table: "RuntimeExtensionPackages",
                columns: new[] { "ExtensionKey", "Version", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_RuntimeIngressConfigurations_IsActive_IngressTransport_IngressKind",
                schema: "Runtime",
                table: "RuntimeIngressConfigurations",
                columns: new[] { "IsActive", "IngressTransport", "IngressKind" });

            migrationBuilder.CreateIndex(
                name: "IX_RuntimeIngressConfigurations_IsActive_RuntimeOrchestrationArtifactId",
                schema: "Runtime",
                table: "RuntimeIngressConfigurations",
                columns: new[] { "IsActive", "RuntimeOrchestrationArtifactId" });

            migrationBuilder.CreateIndex(
                name: "IX_RuntimeIngressConfigurations_RouteTopic_RouteVersion_IsActive",
                schema: "Runtime",
                table: "RuntimeIngressConfigurations",
                columns: new[] { "RouteTopic", "RouteVersion", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_RuntimeIngressConfigurations_RuntimeOrchestrationArtifactId_ConfigurationKey",
                schema: "Runtime",
                table: "RuntimeIngressConfigurations",
                columns: new[] { "RuntimeOrchestrationArtifactId", "ConfigurationKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RuntimeOrchestrationArtifacts_LifecycleStatus_IsActive",
                schema: "Runtime",
                table: "RuntimeOrchestrationArtifacts",
                columns: new[] { "LifecycleStatus", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_RuntimeOrchestrationArtifacts_OrchestrationDefinitionKey_IsActive",
                schema: "Runtime",
                table: "RuntimeOrchestrationArtifacts",
                columns: new[] { "OrchestrationDefinitionKey", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_RuntimeOrchestrationArtifacts_OrchestrationDefinitionKey_Version",
                schema: "Runtime",
                table: "RuntimeOrchestrationArtifacts",
                columns: new[] { "OrchestrationDefinitionKey", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RuntimeOrchestrationArtifacts_Status_IsActive",
                schema: "Runtime",
                table: "RuntimeOrchestrationArtifacts",
                columns: new[] { "Status", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_RuntimeTriggerClaims_RuntimeOrchestrationArtifactId",
                schema: "Runtime",
                table: "RuntimeTriggerClaims",
                column: "RuntimeOrchestrationArtifactId");

            migrationBuilder.CreateIndex(
                name: "IX_SagaFailureEvents_OrchestrationKey_OrchestrationVersion_TransitionType_OccurredOnUtc",
                schema: "Runtime",
                table: "SagaFailureEvents",
                columns: new[] { "OrchestrationKey", "OrchestrationVersion", "TransitionType", "OccurredOnUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SagaFailureEvents_ScopeProjectionVersion_OccurredOnUtc",
                schema: "Runtime",
                table: "SagaFailureEvents",
                columns: new[] { "ScopeProjectionVersion", "OccurredOnUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SagaFailureEvents_TransitionType_OccurredOnUtc",
                schema: "Runtime",
                table: "SagaFailureEvents",
                columns: new[] { "TransitionType", "OccurredOnUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SagaStatisticsObservations_OrchestrationKey_OccurredOnUtc",
                schema: "Runtime",
                table: "SagaStatisticsObservations",
                columns: new[] { "OrchestrationKey", "OccurredOnUtc" },
                filter: "[Processed] = 1 AND [StatusChanged] = 1 AND [IsBackfill] = 0")
                .Annotation("SqlServer:Include", new[] { "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_SagaStatisticsObservations_Pending_InstanceId_Revision",
                schema: "Runtime",
                table: "SagaStatisticsObservations",
                columns: new[] { "InstanceId", "Revision" },
                unique: true,
                filter: "[Processed] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_SagaStatisticsProjections_OrchestrationKey_StartedOnUtc",
                schema: "Runtime",
                table: "SagaStatisticsProjections",
                columns: new[] { "OrchestrationKey", "StartedOnUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SagaStatisticsProjections_Status_StartedOnUtc",
                schema: "Runtime",
                table: "SagaStatisticsProjections",
                columns: new[] { "Status", "StartedOnUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_StageExecutions_OrchestrationInstanceId_StageKey",
                schema: "Runtime",
                table: "StageExecutions",
                columns: new[] { "OrchestrationInstanceId", "StageKey" },
                unique: true);

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
                name: "CIX_TaskExecutions_StorageBucket_OrchestrationInstanceId_Id",
                schema: "Runtime",
                table: "TaskExecutions",
                columns: new[] { "StorageBucket", "OrchestrationInstanceId", "Id" },
                unique: true)
                .Annotation("SqlServer:Clustered", true)
                .Annotation("SqlServer:DataCompression", DataCompressionType.Row)
                .Annotation("SqlServer:FillFactor", 95);

            migrationBuilder.CreateIndex(
                name: "IX_TaskExecutions_CorrelationId",
                schema: "Runtime",
                table: "TaskExecutions",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskExecutions_LegacyWaiting_WaitingSinceUtc_Id",
                schema: "Runtime",
                table: "TaskExecutions",
                columns: new[] { "WaitingSinceUtc", "Id" },
                filter: "[Status] = 'WaitingResponse' AND [ResponseDeadlineUtc] IS NULL")
                .Annotation("SqlServer:Include", new[] { "CorrelationId" });

            migrationBuilder.CreateIndex(
                name: "IX_TaskExecutions_OrchestrationInstanceId",
                schema: "Runtime",
                table: "TaskExecutions",
                column: "OrchestrationInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskExecutions_StageExecutionId_TaskKey",
                schema: "Runtime",
                table: "TaskExecutions",
                columns: new[] { "StageExecutionId", "TaskKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TaskExecutions_Waiting_ResponseDeadlineUtc_Id",
                schema: "Runtime",
                table: "TaskExecutions",
                columns: new[] { "ResponseDeadlineUtc", "Id" },
                filter: "[Status] = 'WaitingResponse' AND [ResponseDeadlineUtc] IS NOT NULL")
                .Annotation("SqlServer:Include", new[] { "CorrelationId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CompensationExecutions",
                schema: "Runtime");

            migrationBuilder.DropTable(
                name: "DataProtectionKeys",
                schema: "Runtime");

            migrationBuilder.DropTable(
                name: "DurableAction",
                schema: "Runtime");

            migrationBuilder.DropTable(
                name: "EnvironmentVariableValues",
                schema: "Runtime");

            migrationBuilder.DropTable(
                name: "ExecutionTransitions",
                schema: "Runtime");

            migrationBuilder.DropTable(
                name: "InstanceVariables",
                schema: "Runtime");

            migrationBuilder.DropTable(
                name: "MuleReplay",
                schema: "Runtime");

            migrationBuilder.DropTable(
                name: "OrchestrationInstances",
                schema: "Runtime");

            migrationBuilder.DropTable(
                name: "RuntimeArtifactDeploymentHistory",
                schema: "Runtime");

            migrationBuilder.DropTable(
                name: "RuntimeDesignNodes",
                schema: "Runtime");

            migrationBuilder.DropTable(
                name: "RuntimeExtensionPackages",
                schema: "Runtime");

            migrationBuilder.DropTable(
                name: "RuntimeIngressConfigurations",
                schema: "Runtime");

            migrationBuilder.DropTable(
                name: "RuntimeOrchestrationArtifacts",
                schema: "Runtime");

            migrationBuilder.DropTable(
                name: "RuntimeTriggerClaims",
                schema: "Runtime");

            migrationBuilder.DropTable(
                name: "SagaFailureBackfillCheckpoints",
                schema: "Runtime");

            migrationBuilder.DropTable(
                name: "SagaFailureEvents",
                schema: "Runtime");

            migrationBuilder.DropTable(
                name: "SagaStatisticsBackfillCheckpoints",
                schema: "Runtime");

            migrationBuilder.DropTable(
                name: "SagaStatisticsObservations",
                schema: "Runtime");

            migrationBuilder.DropTable(
                name: "SagaStatisticsProjections",
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

            migrationBuilder.DropSequence(
                name: "RuntimeStorageSequence",
                schema: "Runtime");
        }
    }
}
