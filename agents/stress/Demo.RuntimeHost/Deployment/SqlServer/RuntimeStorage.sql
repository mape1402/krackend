IF OBJECT_ID(N'[Runtime].[__RuntimeStorageMigrationsHistory]') IS NULL
BEGIN
    IF SCHEMA_ID(N'Runtime') IS NULL EXEC(N'CREATE SCHEMA [Runtime];');
    CREATE TABLE [Runtime].[__RuntimeStorageMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___RuntimeStorageMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO
BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261124214317_InitialPortableSqlServerRuntime'
)
BEGIN
    IF SCHEMA_ID(N'Runtime') IS NULL EXEC(N'CREATE SCHEMA [Runtime];');
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261124214317_InitialPortableSqlServerRuntime'
)
BEGIN
    CREATE SEQUENCE [Runtime].[RuntimeStorageSequence] START WITH 1 INCREMENT BY 1 NO CYCLE;
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261124214317_InitialPortableSqlServerRuntime'
)
BEGIN
    CREATE TABLE [Runtime].[CompensationExecutions] (
        [Id] binary(16) NOT NULL,
        [OrchestrationInstanceId] binary(16) NOT NULL,
        [SourceTaskExecutionId] binary(16) NOT NULL,
        [CompensationTaskKey] nvarchar(256) NOT NULL,
        [Status] nvarchar(64) NOT NULL,
        [StartedOnUtc] datetime2 NULL,
        [CompletedOnUtc] datetime2 NULL,
        [FailedOnUtc] datetime2 NULL,
        [RequestPayload] nvarchar(max) NULL,
        [ResponsePayload] nvarchar(max) NULL,
        [ErrorMessage] nvarchar(4000) NULL,
        [Metadata] nvarchar(max) NULL,
        CONSTRAINT [PK_CompensationExecutions] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261124214317_InitialPortableSqlServerRuntime'
)
BEGIN
    CREATE TABLE [Runtime].[DataProtectionKeys] (
        [Id] int NOT NULL IDENTITY,
        [FriendlyName] nvarchar(256) NULL,
        [Xml] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_DataProtectionKeys] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261124214317_InitialPortableSqlServerRuntime'
)
BEGIN
    CREATE TABLE [Runtime].[DurableAction] (
        [Id] nvarchar(26) NOT NULL,
        [ConcurrencyToken] nvarchar(26) NOT NULL,
        [Key] nvarchar(256) NOT NULL,
        [Lane] nvarchar(128) NOT NULL,
        [Payload] nvarchar(max) NOT NULL,
        [PayloadType] nvarchar(512) NOT NULL,
        [Metadata] nvarchar(max) NULL,
        [CorrelationId] nvarchar(128) NULL,
        [DeduplicationKey] nvarchar(256) NULL,
        [Status] int NOT NULL,
        [Attempts] int NOT NULL,
        [LastError] nvarchar(max) NULL,
        [CreatedOnUtc] datetimeoffset NOT NULL,
        [LockedOnUtc] datetimeoffset NULL,
        [StartedOnUtc] datetimeoffset NULL,
        [NextAttemptOnUtc] datetimeoffset NULL,
        [PendingReason] int NOT NULL,
        [DeferralReasonCode] nvarchar(128) NULL,
        [CompletedOnUtc] datetimeoffset NULL,
        [CancelledOnUtc] datetimeoffset NULL,
        [TerminalOnUtc] datetimeoffset NULL,
        CONSTRAINT [PK_DurableAction] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261124214317_InitialPortableSqlServerRuntime'
)
BEGIN
    CREATE TABLE [Runtime].[EnvironmentVariableValues] (
        [Id] binary(16) NOT NULL,
        [VariableKey] nvarchar(256) NOT NULL,
        [ValueType] nvarchar(64) NOT NULL,
        [Value] nvarchar(max) NULL,
        [IsSensitive] bit NOT NULL,
        [IsResolved] bit NOT NULL,
        [LastValidatedOnUtc] datetime2 NOT NULL,
        [CreatedOnUtc] datetime2 NOT NULL,
        [UpdatedOnUtc] datetime2 NOT NULL,
        [UpdatedBy] nvarchar(128) NULL,
        [Notes] nvarchar(2000) NULL,
        CONSTRAINT [PK_EnvironmentVariableValues] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261124214317_InitialPortableSqlServerRuntime'
)
BEGIN
    CREATE TABLE [Runtime].[ExecutionTransitions] (
        [Id] binary(16) NOT NULL,
        [OrchestrationInstanceId] binary(16) NOT NULL,
        [StageExecutionId] binary(16) NULL,
        [TaskExecutionId] binary(16) NULL,
        [TaskExecutionAttemptId] binary(16) NULL,
        [TransitionType] nvarchar(128) NOT NULL,
        [FromStatus] nvarchar(128) NULL,
        [ToStatus] nvarchar(128) NULL,
        [OccurredOnUtc] datetime2 NOT NULL,
        [Message] nvarchar(4000) NULL,
        [Payload] nvarchar(max) NULL,
        [ProducedBy] nvarchar(256) NULL,
        CONSTRAINT [PK_ExecutionTransitions] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261124214317_InitialPortableSqlServerRuntime'
)
BEGIN
    CREATE TABLE [Runtime].[InstanceVariables] (
        [Id] binary(16) NOT NULL,
        [OrchestrationInstanceId] binary(16) NOT NULL,
        [Key] nvarchar(256) NOT NULL,
        [Scope] nvarchar(64) NOT NULL,
        [ValueType] nvarchar(64) NOT NULL,
        [Value] nvarchar(max) NULL,
        [IsSensitive] bit NOT NULL,
        [SourceType] nvarchar(128) NOT NULL,
        [SourceReference] nvarchar(512) NULL,
        [CreatedOnUtc] datetime2 NOT NULL,
        [UpdatedOnUtc] datetime2 NULL,
        [LastUpdatedBy] nvarchar(128) NULL,
        CONSTRAINT [PK_InstanceVariables] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261124214317_InitialPortableSqlServerRuntime'
)
BEGIN
    CREATE TABLE [Runtime].[MuleReplay] (
        [RequestId] nvarchar(26) NOT NULL,
        [ConcurrencyToken] nvarchar(26) NOT NULL,
        [SourceActionId] nvarchar(26) NOT NULL,
        [ReplayRootActionId] nvarchar(26) NOT NULL,
        [ReplayActionId] nvarchar(26) NOT NULL,
        [ReplaySequence] int NOT NULL,
        [BranchId] nvarchar(26) NOT NULL,
        [ActionKey] nvarchar(256) NOT NULL,
        [Lane] nvarchar(128) NOT NULL,
        [CorrelationId] nvarchar(128) NULL,
        [OriginalDeduplicationKey] nvarchar(256) NULL,
        [Status] int NOT NULL,
        [RequestedBy] nvarchar(256) NULL,
        [Reason] nvarchar(max) NULL,
        [Metadata] nvarchar(max) NULL,
        [RequestedOnUtc] datetimeoffset NOT NULL,
        [ScheduleOnUtc] datetimeoffset NOT NULL,
        [Attempts] int NOT NULL,
        [NextAttemptOnUtc] datetimeoffset NULL,
        [LastError] nvarchar(max) NULL,
        [LockedOnUtc] datetimeoffset NULL,
        [LockToken] nvarchar(26) NOT NULL,
        [MaterializedOnUtc] datetimeoffset NULL,
        [CancelledOnUtc] datetimeoffset NULL,
        CONSTRAINT [PK_MuleReplay] PRIMARY KEY ([RequestId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261124214317_InitialPortableSqlServerRuntime'
)
BEGIN
    CREATE TABLE [Runtime].[OrchestrationInstances] (
        [Id] binary(16) NOT NULL,
        [OrchestrationDefinitionKey] nvarchar(256) NOT NULL,
        [RuntimeOrchestrationArtifactId] binary(16) NOT NULL,
        [MaxInstanceDurationSeconds] int NULL,
        [IdleDurationSeconds] int NULL,
        [ExpiresOnUtc] datetime2 NULL,
        [OrchestrationVersion] nvarchar(64) NULL,
        [TotalTaskCount] int NULL,
        [CompletedTaskCount] int NULL,
        [TriggerIntakeId] binary(16) NOT NULL,
        [StartIdempotencyKey] nvarchar(512) NULL,
        [CorrelationId] nvarchar(256) NOT NULL,
        [SagaId] nvarchar(256) NOT NULL,
        [ExecutionKey] nvarchar(512) NOT NULL,
        [Status] nvarchar(64) NOT NULL,
        [CurrentStageKey] nvarchar(256) NULL,
        [CurrentTaskKey] nvarchar(256) NULL,
        [LastExecutedStageKey] nvarchar(256) NULL,
        [LastExecutedTaskKey] nvarchar(256) NULL,
        [CurrentParallelGroupKey] nvarchar(256) NULL,
        [StartedOnUtc] datetime2 NOT NULL,
        [LastUpdatedOnUtc] datetime2 NOT NULL,
        [StatisticsRevision] bigint NOT NULL DEFAULT CAST(0 AS bigint),
        [DispatchCancellationPending] bit NOT NULL,
        [WaitingSinceUtc] datetime2 NULL,
        [IdleSinceUtc] datetime2 NULL,
        [IdleExpiresOnUtc] datetime2 NULL,
        [CompletedOnUtc] datetime2 NULL,
        [FailedOnUtc] datetime2 NULL,
        [StoppedOnUtc] datetime2 NULL,
        [CompensationStartedOnUtc] datetime2 NULL,
        [CompensatedOnUtc] datetime2 NULL,
        [FinalOutcome] nvarchar(128) NULL,
        [ErrorSummary] nvarchar(4000) NULL,
        [RetryCount] int NOT NULL,
        [ActiveLeaseId] nvarchar(256) NULL,
        [ActiveLeaseExpiresOnUtc] datetime2 NULL,
        [SnapshotPayload] nvarchar(max) NULL,
        [Metadata] nvarchar(max) NULL,
        CONSTRAINT [PK_OrchestrationInstances] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261124214317_InitialPortableSqlServerRuntime'
)
BEGIN
    CREATE TABLE [Runtime].[RuntimeArtifactDeploymentHistory] (
        [Id] binary(16) NOT NULL,
        [RuntimeOrchestrationArtifactId] binary(16) NOT NULL,
        [OrchestrationDefinitionKey] nvarchar(256) NOT NULL,
        [OrchestrationVersion] nvarchar(64) NOT NULL,
        [ControlPlaneArtifactId] nvarchar(128) NOT NULL,
        [ReleaseId] nvarchar(128) NULL,
        [ArtifactType] nvarchar(128) NOT NULL,
        [ReleaseStrategy] nvarchar(64) NOT NULL,
        [LifecycleStatus] nvarchar(64) NOT NULL,
        [RuntimeStatus] nvarchar(64) NOT NULL,
        [AppliedOnUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_RuntimeArtifactDeploymentHistory] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261124214317_InitialPortableSqlServerRuntime'
)
BEGIN
    CREATE TABLE [Runtime].[RuntimeDesignNodes] (
        [Id] binary(16) NOT NULL,
        [Key] nvarchar(128) NOT NULL,
        [Name] nvarchar(256) NOT NULL,
        [EndpointBaseUri] nvarchar(1024) NOT NULL,
        [RemoteRuntimeNodeId] nvarchar(128) NOT NULL,
        [DistributionMode] nvarchar(64) NOT NULL,
        [AccessTokenTtlSeconds] int NOT NULL,
        [TokenRefreshSkewSeconds] int NOT NULL,
        [TokenValidationCacheTtlSeconds] int NOT NULL,
        [InboundClientId] nvarchar(256) NULL,
        [InboundKeyId] nvarchar(256) NULL,
        [InboundSecretHash] nvarchar(max) NULL,
        [InboundAllowedScopes] nvarchar(512) NULL,
        [InboundCredentialStatus] nvarchar(64) NOT NULL,
        [InboundCredentialCreatedAtUtc] datetime2 NULL,
        [InboundCredentialRotatedAtUtc] datetime2 NULL,
        [InboundCredentialRevokedAtUtc] datetime2 NULL,
        [InboundLastTokenIssuedAtUtc] datetime2 NULL,
        [InboundLastTokenFailedAtUtc] datetime2 NULL,
        [InboundLastFailureReason] nvarchar(2000) NULL,
        [OutboundClientId] nvarchar(256) NULL,
        [OutboundKeyId] nvarchar(256) NULL,
        [ProtectedOutboundSecret] nvarchar(max) NULL,
        [OutboundRequestedScopes] nvarchar(512) NULL,
        [OutboundCredentialStatus] nvarchar(64) NOT NULL,
        [OutboundCredentialImportedAtUtc] datetime2 NULL,
        [OutboundLastTokenReceivedAtUtc] datetime2 NULL,
        [Description] nvarchar(2000) NULL,
        [Status] nvarchar(64) NOT NULL DEFAULT N'Pending',
        [IsEnabled] bit NOT NULL,
        [CreatedOnUtc] datetime2 NOT NULL,
        [UpdatedOnUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_RuntimeDesignNodes] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261124214317_InitialPortableSqlServerRuntime'
)
BEGIN
    CREATE TABLE [Runtime].[RuntimeExtensionPackages] (
        [Id] binary(16) NOT NULL,
        [BundleId] nvarchar(256) NOT NULL,
        [ExtensionKey] nvarchar(256) NOT NULL,
        [Version] nvarchar(64) NOT NULL,
        [Sha256] nvarchar(256) NOT NULL,
        [SizeBytes] bigint NOT NULL,
        [Manifest] nvarchar(max) NOT NULL,
        [Status] nvarchar(64) NOT NULL,
        [CreatedOnUtc] datetime2 NOT NULL,
        [UpdatedOnUtc] datetime2 NOT NULL,
        [ActivatedOnUtc] datetime2 NULL,
        [StatusReason] nvarchar(4000) NULL,
        CONSTRAINT [PK_RuntimeExtensionPackages] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261124214317_InitialPortableSqlServerRuntime'
)
BEGIN
    CREATE TABLE [Runtime].[RuntimeIngressConfigurations] (
        [Id] binary(16) NOT NULL,
        [RuntimeOrchestrationArtifactId] binary(16) NOT NULL,
        [ConfigurationKey] nvarchar(256) NOT NULL,
        [IngressKind] nvarchar(64) NOT NULL,
        [MessageDisposition] nvarchar(64) NOT NULL,
        [IngressTransport] nvarchar(64) NOT NULL,
        [SettingsPayload] nvarchar(max) NOT NULL,
        [RouteTopic] nvarchar(512) NULL,
        [RouteVersion] nvarchar(64) NULL,
        [IsActive] bit NOT NULL,
        [CreatedOnUtc] datetime2 NOT NULL,
        [UpdatedOnUtc] datetime2 NOT NULL,
        [DeactivatedOnUtc] datetime2 NULL,
        CONSTRAINT [PK_RuntimeIngressConfigurations] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261124214317_InitialPortableSqlServerRuntime'
)
BEGIN
    CREATE TABLE [Runtime].[RuntimeOrchestrationArtifacts] (
        [Id] binary(16) NOT NULL,
        [OrchestrationDefinitionKey] nvarchar(256) NOT NULL,
        [ArtifactType] nvarchar(128) NOT NULL,
        [SourceOrchestrationVersionId] binary(16) NOT NULL,
        [Version] nvarchar(64) NOT NULL,
        [ArtifactChecksum] nvarchar(256) NOT NULL,
        [ArtifactPayload] nvarchar(max) NOT NULL,
        [Status] nvarchar(64) NOT NULL DEFAULT N'Pending',
        [LifecycleStatus] nvarchar(64) NOT NULL DEFAULT N'Active',
        [LastReleaseId] nvarchar(128) NULL,
        [LastControlPlaneArtifactId] nvarchar(128) NULL,
        [ReleaseStrategy] nvarchar(64) NULL,
        [IngressGeneration] bigint NOT NULL,
        [IsActive] bit NOT NULL,
        [LoadedToCache] bit NOT NULL,
        [DeployedOnUtc] datetime2 NOT NULL,
        [ActivatedOnUtc] datetime2 NULL,
        [ProjectionStartedOnUtc] datetime2 NULL,
        [ProjectionCompletedOnUtc] datetime2 NULL,
        [ProjectionFailedOnUtc] datetime2 NULL,
        [ProjectionError] nvarchar(4000) NULL,
        [RetiredOnUtc] datetime2 NULL,
        [SupersededByArtifactId] binary(16) NULL,
        [Notes] nvarchar(2000) NULL,
        CONSTRAINT [PK_RuntimeOrchestrationArtifacts] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261124214317_InitialPortableSqlServerRuntime'
)
BEGIN
    CREATE TABLE [Runtime].[RuntimeTriggerClaims] (
        [Id] nvarchar(1024) NOT NULL,
        [RuntimeOrchestrationArtifactId] binary(16) NOT NULL,
        [UpdatedOnUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_RuntimeTriggerClaims] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261124214317_InitialPortableSqlServerRuntime'
)
BEGIN
    CREATE TABLE [Runtime].[SagaFailureBackfillCheckpoints] (
        [Id] nvarchar(64) NOT NULL,
        [BeforeUtc] datetime2 NOT NULL,
        [CursorUtc] datetime2 NOT NULL,
        [CursorOffset] int NOT NULL,
        [Completed] bit NOT NULL,
        [ConcurrencyToken] nvarchar(32) NOT NULL,
        CONSTRAINT [PK_SagaFailureBackfillCheckpoints] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261124214317_InitialPortableSqlServerRuntime'
)
BEGIN
    CREATE TABLE [Runtime].[SagaFailureEvents] (
        [Id] binary(16) NOT NULL,
        [OrchestrationInstanceId] binary(16) NOT NULL,
        [OccurredOnUtc] datetime2 NOT NULL,
        [TransitionType] nvarchar(128) NOT NULL,
        [OrchestrationKey] nvarchar(256) NULL,
        [OrchestrationVersion] nvarchar(64) NULL,
        [ScopeProjectionVersion] int NULL,
        CONSTRAINT [PK_SagaFailureEvents] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261124214317_InitialPortableSqlServerRuntime'
)
BEGIN
    CREATE TABLE [Runtime].[SagaStatisticsBackfillCheckpoints] (
        [Id] nvarchar(64) NOT NULL,
        [CompletedOnUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_SagaStatisticsBackfillCheckpoints] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261124214317_InitialPortableSqlServerRuntime'
)
BEGIN
    CREATE TABLE [Runtime].[SagaStatisticsObservations] (
        [Id] nvarchar(64) NOT NULL,
        [InstanceId] binary(16) NOT NULL,
        [ArtifactId] binary(16) NOT NULL,
        [OrchestrationKey] nvarchar(256) NOT NULL,
        [Revision] bigint NOT NULL,
        [Status] nvarchar(64) NOT NULL,
        [StageKey] nvarchar(256) NULL,
        [StartedOnUtc] datetime2 NOT NULL,
        [OccurredOnUtc] datetime2 NOT NULL,
        [Processed] bit NOT NULL,
        [StatusChanged] bit NOT NULL,
        [StatusFirstReached] bit NOT NULL,
        [IsBackfill] bit NOT NULL,
        CONSTRAINT [PK_SagaStatisticsObservations] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261124214317_InitialPortableSqlServerRuntime'
)
BEGIN
    CREATE TABLE [Runtime].[SagaStatisticsProjections] (
        [Id] nvarchar(26) NOT NULL,
        [ArtifactId] binary(16) NOT NULL,
        [OrchestrationKey] nvarchar(256) NOT NULL,
        [Revision] bigint NOT NULL,
        [Status] nvarchar(64) NOT NULL,
        [StartedOnUtc] datetime2 NOT NULL,
        [LastObservedOnUtc] datetime2 NOT NULL,
        [ReachedStatuses] bigint NOT NULL,
        CONSTRAINT [PK_SagaStatisticsProjections] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261124214317_InitialPortableSqlServerRuntime'
)
BEGIN
    CREATE TABLE [Runtime].[StageExecutions] (
        [Id] binary(16) NOT NULL,
        [OrchestrationInstanceId] binary(16) NOT NULL,
        [StageKey] nvarchar(256) NOT NULL,
        [Order] int NOT NULL,
        [Status] nvarchar(64) NOT NULL,
        [WasSkipped] bit NOT NULL,
        [SkipReason] nvarchar(2000) NULL,
        [ExecutionConditionResult] bit NULL,
        [StartedOnUtc] datetime2 NULL,
        [CompletedOnUtc] datetime2 NULL,
        [FailedOnUtc] datetime2 NULL,
        [ErrorSummary] nvarchar(4000) NULL,
        [ParallelGroupCount] int NOT NULL,
        [Metadata] nvarchar(max) NULL,
        CONSTRAINT [PK_StageExecutions] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261124214317_InitialPortableSqlServerRuntime'
)
BEGIN
    CREATE TABLE [Runtime].[TaskDispatches] (
        [Id] binary(16) NOT NULL,
        [TaskExecutionAttemptId] binary(16) NOT NULL,
        [DispatchType] nvarchar(64) NOT NULL,
        [Destination] nvarchar(512) NULL,
        [RequestPayload] nvarchar(max) NULL,
        [DispatchStatus] nvarchar(64) NOT NULL,
        [CommandId] nvarchar(256) NULL,
        [CorrelationId] nvarchar(256) NULL,
        [SentOnUtc] datetime2 NULL,
        [CallbackReceiptId] nvarchar(64) NULL,
        [CallbackAcceptedOnUtc] datetime2 NULL,
        [PendingCallbackPayload] nvarchar(max) NULL,
        [CallbackPending] bit NOT NULL,
        [ScheduledOnUtc] datetime2 NULL,
        [AcknowledgedOnUtc] datetime2 NULL,
        [FailedOnUtc] datetime2 NULL,
        [FailureReason] nvarchar(4000) NULL,
        [PhysicalCancellationPending] bit NOT NULL,
        [PhysicalCancellationAttemptCount] int NOT NULL,
        [PhysicalCancellationLastAttemptOnUtc] datetime2 NULL,
        [PhysicalCancellationCompletedOnUtc] datetime2 NULL,
        [Metadata] nvarchar(max) NULL,
        [StorageSequence] bigint NOT NULL DEFAULT (NEXT VALUE FOR [Runtime].[RuntimeStorageSequence]),
        CONSTRAINT [PK_TaskDispatches] PRIMARY KEY NONCLUSTERED ([Id]) WITH (FILLFACTOR = 95)
    );
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261124214317_InitialPortableSqlServerRuntime'
)
BEGIN
    CREATE TABLE [Runtime].[TaskExecutionAttempts] (
        [Id] binary(16) NOT NULL,
        [TaskExecutionId] binary(16) NOT NULL,
        [AttemptNumber] int NOT NULL,
        [Status] nvarchar(64) NOT NULL,
        [StartedOnUtc] datetime2 NULL,
        [WaitingSinceUtc] datetime2 NULL,
        [CompletedOnUtc] datetime2 NULL,
        [FailedOnUtc] datetime2 NULL,
        [TimedOutOnUtc] datetime2 NULL,
        [RequestPayload] nvarchar(max) NULL,
        [ResponsePayload] nvarchar(max) NULL,
        [ErrorCode] nvarchar(256) NULL,
        [ErrorMessage] nvarchar(4000) NULL,
        [DispatchId] binary(16) NULL,
        [Metadata] nvarchar(max) NULL,
        [StorageSequence] bigint NOT NULL DEFAULT (NEXT VALUE FOR [Runtime].[RuntimeStorageSequence]),
        CONSTRAINT [PK_TaskExecutionAttempts] PRIMARY KEY NONCLUSTERED ([Id]) WITH (FILLFACTOR = 95)
    );
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261124214317_InitialPortableSqlServerRuntime'
)
BEGIN
    CREATE TABLE [Runtime].[TaskExecutions] (
        [Id] binary(16) NOT NULL,
        [OrchestrationInstanceId] binary(16) NOT NULL,
        [StageExecutionId] binary(16) NOT NULL,
        [TaskKey] nvarchar(256) NOT NULL,
        [TaskKind] nvarchar(64) NOT NULL,
        [ExecutionMode] nvarchar(64) NOT NULL,
        [ParallelGroupId] binary(16) NULL,
        [Status] nvarchar(64) NOT NULL,
        [WasSkipped] bit NOT NULL,
        [SkipReason] nvarchar(2000) NULL,
        [ExecutionConditionResult] bit NULL,
        [OnErrorPolicy] nvarchar(64) NOT NULL,
        [AwaitResponse] bit NOT NULL,
        [StartedOnUtc] datetime2 NULL,
        [WaitingSinceUtc] datetime2 NULL,
        [ResponseDeadlineUtc] datetime2 NULL,
        [TimeoutActionId] nvarchar(64) NULL,
        [TimeoutActionLane] nvarchar(128) NULL,
        [CompletedOnUtc] datetime2 NULL,
        [FailedOnUtc] datetime2 NULL,
        [TimedOutOnUtc] datetime2 NULL,
        [LastAttemptNumber] int NOT NULL,
        [OutputVariablesPayload] nvarchar(max) NULL,
        [CorrelationId] nvarchar(256) NULL,
        [Metadata] nvarchar(max) NULL,
        [StorageBucket] AS CONVERT(tinyint, CONVERT(int, SUBSTRING([OrchestrationInstanceId], 16, 1)) & 63) PERSISTED,
        CONSTRAINT [PK_TaskExecutions] PRIMARY KEY NONCLUSTERED ([Id]) WITH (FILLFACTOR = 95)
    );
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261124214317_InitialPortableSqlServerRuntime'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CompensationExecutions_Instance_SourceTask_CompensationTask] ON [Runtime].[CompensationExecutions] ([OrchestrationInstanceId], [SourceTaskExecutionId], [CompensationTaskKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261124214317_InitialPortableSqlServerRuntime'
)
BEGIN
    CREATE INDEX [IX_CompensationExecutions_OrchestrationInstanceId] ON [Runtime].[CompensationExecutions] ([OrchestrationInstanceId]);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261124214317_InitialPortableSqlServerRuntime'
)
BEGIN
    CREATE INDEX [IX_DurableAction_CorrelationId] ON [Runtime].[DurableAction] ([CorrelationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261124214317_InitialPortableSqlServerRuntime'
)
BEGIN
    CREATE INDEX [IX_DurableAction_Key_DeduplicationKey] ON [Runtime].[DurableAction] ([Key], [DeduplicationKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261124214317_InitialPortableSqlServerRuntime'
)
BEGIN
    CREATE INDEX [IX_DurableAction_Lane_Status_NextAttemptOnUtc_CreatedOnUtc] ON [Runtime].[DurableAction] ([Lane], [Status], [NextAttemptOnUtc], [CreatedOnUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261124214317_InitialPortableSqlServerRuntime'
)
BEGIN
    CREATE INDEX [IX_DurableAction_Status_CompletedOnUtc] ON [Runtime].[DurableAction] ([Status], [CompletedOnUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261124214317_InitialPortableSqlServerRuntime'
)
BEGIN
    CREATE INDEX [IX_DurableAction_Status_LockedOnUtc] ON [Runtime].[DurableAction] ([Status], [LockedOnUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261124214317_InitialPortableSqlServerRuntime'
)
BEGIN
    CREATE INDEX [IX_DurableAction_Status_PendingReason_NextAttemptOnUtc] ON [Runtime].[DurableAction] ([Status], [PendingReason], [NextAttemptOnUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261124214317_InitialPortableSqlServerRuntime'
)
BEGIN
    CREATE UNIQUE INDEX [IX_EnvironmentVariableValues_VariableKey] ON [Runtime].[EnvironmentVariableValues] ([VariableKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261124214317_InitialPortableSqlServerRuntime'
)
BEGIN
    CREATE INDEX [IX_ExecutionTransitions_OccurredOnUtc] ON [Runtime].[ExecutionTransitions] ([OccurredOnUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261124214317_InitialPortableSqlServerRuntime'
)
BEGIN
    CREATE INDEX [IX_ExecutionTransitions_OrchestrationInstanceId_OccurredOnUtc] ON [Runtime].[ExecutionTransitions] ([OrchestrationInstanceId], [OccurredOnUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261124214317_InitialPortableSqlServerRuntime'
)
BEGIN
    CREATE UNIQUE INDEX [IX_InstanceVariables_OrchestrationInstanceId_Key] ON [Runtime].[InstanceVariables] ([OrchestrationInstanceId], [Key]);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261124214317_InitialPortableSqlServerRuntime'
)
BEGIN
    CREATE UNIQUE INDEX [IX_MuleReplay_ReplayActionId] ON [Runtime].[MuleReplay] ([ReplayActionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261124214317_InitialPortableSqlServerRuntime'
)
BEGIN
    CREATE UNIQUE INDEX [IX_MuleReplay_SourceActionId_BranchId] ON [Runtime].[MuleReplay] ([SourceActionId], [BranchId]);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261124214317_InitialPortableSqlServerRuntime'
)
BEGIN
    CREATE INDEX [IX_MuleReplay_Status_LockedOnUtc] ON [Runtime].[MuleReplay] ([Status], [LockedOnUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261124214317_InitialPortableSqlServerRuntime'
)
BEGIN
    CREATE INDEX [IX_MuleReplay_Status_ScheduleOnUtc_NextAttemptOnUtc] ON [Runtime].[MuleReplay] ([Status], [ScheduleOnUtc], [NextAttemptOnUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261124214317_InitialPortableSqlServerRuntime'
)
BEGIN
    CREATE INDEX [IX_OrchestrationInstances_CorrelationId] ON [Runtime].[OrchestrationInstances] ([CorrelationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261124214317_InitialPortableSqlServerRuntime'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_OrchestrationInstances_DispatchCancellationPending_Id] ON [Runtime].[OrchestrationInstances] ([Id]) WHERE [DispatchCancellationPending] = 1');
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261124214317_InitialPortableSqlServerRuntime'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_OrchestrationInstances_Idle_IdleExpiresOnUtc_Id] ON [Runtime].[OrchestrationInstances] ([IdleExpiresOnUtc], [Id]) INCLUDE ([CorrelationId]) WHERE [Status] = ''Idle'' AND [IdleExpiresOnUtc] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261124214317_InitialPortableSqlServerRuntime'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_OrchestrationInstances_Running_ExpiresOnUtc_Id] ON [Runtime].[OrchestrationInstances] ([ExpiresOnUtc], [Id]) INCLUDE ([CorrelationId]) WHERE [Status] = ''Running'' AND [ExpiresOnUtc] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261124214317_InitialPortableSqlServerRuntime'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_OrchestrationInstances_Running_StartedOnUtc_Id] ON [Runtime].[OrchestrationInstances] ([StartedOnUtc] DESC, [Id] DESC) INCLUDE ([CorrelationId]) WHERE [Status] = ''Running''');
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261124214317_InitialPortableSqlServerRuntime'
)
BEGIN
    CREATE INDEX [IX_OrchestrationInstances_StartedOnUtc_Id] ON [Runtime].[OrchestrationInstances] ([StartedOnUtc] DESC, [Id] DESC) INCLUDE ([CorrelationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261124214317_InitialPortableSqlServerRuntime'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_OrchestrationInstances_StartIdempotencyKey] ON [Runtime].[OrchestrationInstances] ([StartIdempotencyKey]) WHERE [StartIdempotencyKey] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261124214317_InitialPortableSqlServerRuntime'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_OrchestrationInstances_StatisticsRevision] ON [Runtime].[OrchestrationInstances] ([StatisticsRevision]) WHERE [StatisticsRevision] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261124214317_InitialPortableSqlServerRuntime'
)
BEGIN
    CREATE UNIQUE INDEX [IX_RuntimeArtifactDeploymentHistory_ControlPlaneArtifactId] ON [Runtime].[RuntimeArtifactDeploymentHistory] ([ControlPlaneArtifactId]);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261124214317_InitialPortableSqlServerRuntime'
)
BEGIN
    CREATE INDEX [IX_RuntimeArtifactDeploymentHistory_OrchestrationDefinitionKey_AppliedOnUtc] ON [Runtime].[RuntimeArtifactDeploymentHistory] ([OrchestrationDefinitionKey], [AppliedOnUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261124214317_InitialPortableSqlServerRuntime'
)
BEGIN
    CREATE INDEX [IX_RuntimeArtifactDeploymentHistory_ReleaseId] ON [Runtime].[RuntimeArtifactDeploymentHistory] ([ReleaseId]);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261124214317_InitialPortableSqlServerRuntime'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_RuntimeDesignNodes_InboundClientId] ON [Runtime].[RuntimeDesignNodes] ([InboundClientId]) WHERE [InboundClientId] IS NOT NULL AND [InboundClientId] <> ''''');
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261124214317_InitialPortableSqlServerRuntime'
)
BEGIN
    CREATE INDEX [IX_RuntimeDesignNodes_IsEnabled] ON [Runtime].[RuntimeDesignNodes] ([IsEnabled]);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261124214317_InitialPortableSqlServerRuntime'
)
BEGIN
    CREATE UNIQUE INDEX [IX_RuntimeDesignNodes_Key] ON [Runtime].[RuntimeDesignNodes] ([Key]);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261124214317_InitialPortableSqlServerRuntime'
)
BEGIN
    CREATE INDEX [IX_RuntimeDesignNodes_Status] ON [Runtime].[RuntimeDesignNodes] ([Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261124214317_InitialPortableSqlServerRuntime'
)
BEGIN
    CREATE UNIQUE INDEX [IX_RuntimeExtensionPackages_BundleId_ExtensionKey_Version] ON [Runtime].[RuntimeExtensionPackages] ([BundleId], [ExtensionKey], [Version]);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261124214317_InitialPortableSqlServerRuntime'
)
BEGIN
    CREATE INDEX [IX_RuntimeExtensionPackages_ExtensionKey_Version_Status] ON [Runtime].[RuntimeExtensionPackages] ([ExtensionKey], [Version], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261124214317_InitialPortableSqlServerRuntime'
)
BEGIN
    CREATE INDEX [IX_RuntimeIngressConfigurations_IsActive_IngressTransport_IngressKind] ON [Runtime].[RuntimeIngressConfigurations] ([IsActive], [IngressTransport], [IngressKind]);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261124214317_InitialPortableSqlServerRuntime'
)
BEGIN
    CREATE INDEX [IX_RuntimeIngressConfigurations_IsActive_RuntimeOrchestrationArtifactId] ON [Runtime].[RuntimeIngressConfigurations] ([IsActive], [RuntimeOrchestrationArtifactId]);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261124214317_InitialPortableSqlServerRuntime'
)
BEGIN
    CREATE INDEX [IX_RuntimeIngressConfigurations_RouteTopic_RouteVersion_IsActive] ON [Runtime].[RuntimeIngressConfigurations] ([RouteTopic], [RouteVersion], [IsActive]);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261124214317_InitialPortableSqlServerRuntime'
)
BEGIN
    CREATE UNIQUE INDEX [IX_RuntimeIngressConfigurations_RuntimeOrchestrationArtifactId_ConfigurationKey] ON [Runtime].[RuntimeIngressConfigurations] ([RuntimeOrchestrationArtifactId], [ConfigurationKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261124214317_InitialPortableSqlServerRuntime'
)
BEGIN
    CREATE INDEX [IX_RuntimeOrchestrationArtifacts_LifecycleStatus_IsActive] ON [Runtime].[RuntimeOrchestrationArtifacts] ([LifecycleStatus], [IsActive]);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261124214317_InitialPortableSqlServerRuntime'
)
BEGIN
    CREATE INDEX [IX_RuntimeOrchestrationArtifacts_OrchestrationDefinitionKey_IsActive] ON [Runtime].[RuntimeOrchestrationArtifacts] ([OrchestrationDefinitionKey], [IsActive]);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261124214317_InitialPortableSqlServerRuntime'
)
BEGIN
    CREATE UNIQUE INDEX [IX_RuntimeOrchestrationArtifacts_OrchestrationDefinitionKey_Version] ON [Runtime].[RuntimeOrchestrationArtifacts] ([OrchestrationDefinitionKey], [Version]);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261124214317_InitialPortableSqlServerRuntime'
)
BEGIN
    CREATE INDEX [IX_RuntimeOrchestrationArtifacts_Status_IsActive] ON [Runtime].[RuntimeOrchestrationArtifacts] ([Status], [IsActive]);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261124214317_InitialPortableSqlServerRuntime'
)
BEGIN
    CREATE INDEX [IX_RuntimeTriggerClaims_RuntimeOrchestrationArtifactId] ON [Runtime].[RuntimeTriggerClaims] ([RuntimeOrchestrationArtifactId]);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261124214317_InitialPortableSqlServerRuntime'
)
BEGIN
    CREATE INDEX [IX_SagaFailureEvents_OrchestrationKey_OrchestrationVersion_TransitionType_OccurredOnUtc] ON [Runtime].[SagaFailureEvents] ([OrchestrationKey], [OrchestrationVersion], [TransitionType], [OccurredOnUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261124214317_InitialPortableSqlServerRuntime'
)
BEGIN
    CREATE INDEX [IX_SagaFailureEvents_ScopeProjectionVersion_OccurredOnUtc] ON [Runtime].[SagaFailureEvents] ([ScopeProjectionVersion], [OccurredOnUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261124214317_InitialPortableSqlServerRuntime'
)
BEGIN
    CREATE INDEX [IX_SagaFailureEvents_TransitionType_OccurredOnUtc] ON [Runtime].[SagaFailureEvents] ([TransitionType], [OccurredOnUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261124214317_InitialPortableSqlServerRuntime'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_SagaStatisticsObservations_OrchestrationKey_OccurredOnUtc] ON [Runtime].[SagaStatisticsObservations] ([OrchestrationKey], [OccurredOnUtc]) INCLUDE ([Status]) WHERE [Processed] = 1 AND [StatusChanged] = 1 AND [IsBackfill] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261124214317_InitialPortableSqlServerRuntime'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_SagaStatisticsObservations_Pending_InstanceId_Revision] ON [Runtime].[SagaStatisticsObservations] ([InstanceId], [Revision]) WHERE [Processed] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261124214317_InitialPortableSqlServerRuntime'
)
BEGIN
    CREATE INDEX [IX_SagaStatisticsProjections_OrchestrationKey_StartedOnUtc] ON [Runtime].[SagaStatisticsProjections] ([OrchestrationKey], [StartedOnUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261124214317_InitialPortableSqlServerRuntime'
)
BEGIN
    CREATE INDEX [IX_SagaStatisticsProjections_Status_StartedOnUtc] ON [Runtime].[SagaStatisticsProjections] ([Status], [StartedOnUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261124214317_InitialPortableSqlServerRuntime'
)
BEGIN
    CREATE UNIQUE INDEX [IX_StageExecutions_OrchestrationInstanceId_StageKey] ON [Runtime].[StageExecutions] ([OrchestrationInstanceId], [StageKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261124214317_InitialPortableSqlServerRuntime'
)
BEGIN
    CREATE UNIQUE CLUSTERED INDEX [CIX_TaskDispatches_StorageSequence] ON [Runtime].[TaskDispatches] ([StorageSequence]) WITH (FILLFACTOR = 95, DATA_COMPRESSION = ROW);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261124214317_InitialPortableSqlServerRuntime'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_TaskDispatches_CallbackPending_CallbackAcceptedOnUtc] ON [Runtime].[TaskDispatches] ([CallbackPending], [CallbackAcceptedOnUtc]) WHERE [CallbackPending] = 1 WITH (FILLFACTOR = 95, DATA_COMPRESSION = ROW)');
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261124214317_InitialPortableSqlServerRuntime'
)
BEGIN
    CREATE INDEX [IX_TaskDispatches_CommandId] ON [Runtime].[TaskDispatches] ([CommandId]) WITH (FILLFACTOR = 95, DATA_COMPRESSION = ROW);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261124214317_InitialPortableSqlServerRuntime'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_TaskDispatches_PhysicalCancellationPending] ON [Runtime].[TaskDispatches] ([PhysicalCancellationPending]) WHERE [PhysicalCancellationPending] = 1 WITH (FILLFACTOR = 95, DATA_COMPRESSION = ROW)');
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261124214317_InitialPortableSqlServerRuntime'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_TaskDispatches_QueuePending_Id_ScheduledOnUtc] ON [Runtime].[TaskDispatches] ([ScheduledOnUtc], [Id]) WHERE [DispatchStatus] = ''QueuePending'' WITH (FILLFACTOR = 95, DATA_COMPRESSION = ROW)');
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261124214317_InitialPortableSqlServerRuntime'
)
BEGIN
    CREATE UNIQUE INDEX [IX_TaskDispatches_TaskExecutionAttemptId] ON [Runtime].[TaskDispatches] ([TaskExecutionAttemptId]) WITH (FILLFACTOR = 95, DATA_COMPRESSION = ROW);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261124214317_InitialPortableSqlServerRuntime'
)
BEGIN
    CREATE UNIQUE CLUSTERED INDEX [CIX_TaskExecutionAttempts_StorageSequence] ON [Runtime].[TaskExecutionAttempts] ([StorageSequence]) WITH (FILLFACTOR = 95, DATA_COMPRESSION = ROW);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261124214317_InitialPortableSqlServerRuntime'
)
BEGIN
    CREATE INDEX [IX_TaskExecutionAttempts_DispatchId] ON [Runtime].[TaskExecutionAttempts] ([DispatchId]) WITH (FILLFACTOR = 95, DATA_COMPRESSION = ROW);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261124214317_InitialPortableSqlServerRuntime'
)
BEGIN
    CREATE UNIQUE INDEX [IX_TaskExecutionAttempts_TaskExecutionId_AttemptNumber] ON [Runtime].[TaskExecutionAttempts] ([TaskExecutionId], [AttemptNumber]) WITH (FILLFACTOR = 95, DATA_COMPRESSION = ROW);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261124214317_InitialPortableSqlServerRuntime'
)
BEGIN
    CREATE UNIQUE CLUSTERED INDEX [CIX_TaskExecutions_StorageBucket_OrchestrationInstanceId_Id] ON [Runtime].[TaskExecutions] ([StorageBucket], [OrchestrationInstanceId], [Id]) WITH (FILLFACTOR = 95, DATA_COMPRESSION = ROW);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261124214317_InitialPortableSqlServerRuntime'
)
BEGIN
    CREATE INDEX [IX_TaskExecutions_CorrelationId] ON [Runtime].[TaskExecutions] ([CorrelationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261124214317_InitialPortableSqlServerRuntime'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_TaskExecutions_LegacyWaiting_WaitingSinceUtc_Id] ON [Runtime].[TaskExecutions] ([WaitingSinceUtc], [Id]) INCLUDE ([CorrelationId]) WHERE [Status] = ''WaitingResponse'' AND [ResponseDeadlineUtc] IS NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261124214317_InitialPortableSqlServerRuntime'
)
BEGIN
    CREATE INDEX [IX_TaskExecutions_OrchestrationInstanceId] ON [Runtime].[TaskExecutions] ([OrchestrationInstanceId]);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261124214317_InitialPortableSqlServerRuntime'
)
BEGIN
    CREATE UNIQUE INDEX [IX_TaskExecutions_StageExecutionId_TaskKey] ON [Runtime].[TaskExecutions] ([StageExecutionId], [TaskKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261124214317_InitialPortableSqlServerRuntime'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_TaskExecutions_Waiting_ResponseDeadlineUtc_Id] ON [Runtime].[TaskExecutions] ([ResponseDeadlineUtc], [Id]) INCLUDE ([CorrelationId]) WHERE [Status] = ''WaitingResponse'' AND [ResponseDeadlineUtc] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261124214317_InitialPortableSqlServerRuntime'
)
BEGIN
    INSERT INTO [Runtime].[__RuntimeStorageMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261124214317_InitialPortableSqlServerRuntime', N'10.0.10');
END;

COMMIT;
GO

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261124214346_EnablePortableSqlServerDatabaseSettings'
)
BEGIN
    DECLARE @databaseName nvarchar(258) = QUOTENAME(DB_NAME());
    DECLARE @engineEdition int = CONVERT(int, SERVERPROPERTY(N'EngineEdition'));

    IF EXISTS (
        SELECT 1 FROM sys.databases
        WHERE database_id = DB_ID()
          AND is_read_committed_snapshot_on = 0)
    BEGIN
        IF @engineEdition = 5
            THROW 51010, 'READ_COMMITTED_SNAPSHOT must be enabled from the Azure SQL logical master database.', 1;

        EXEC(N'ALTER DATABASE ' + @databaseName +
            N' SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE;');
    END;

    EXEC(N'ALTER DATABASE ' + @databaseName +
        N' SET TARGET_RECOVERY_TIME = 300 SECONDS;');
    EXEC(N'ALTER DATABASE ' + @databaseName +
        N' SET AUTO_UPDATE_STATISTICS_ASYNC ON;');

    -- SQL Server 2019+ and Azure SQL can reduce last-page insert contention.
    -- Dynamic SQL keeps the same migration executable on older editions.
    IF TRY_CONVERT(int, SERVERPROPERTY(N'ProductMajorVersion')) >= 15
    BEGIN
        EXEC(N'ALTER INDEX [PK_ExecutionTransitions] ON [Runtime].[ExecutionTransitions] SET (OPTIMIZE_FOR_SEQUENTIAL_KEY = ON);');
        EXEC(N'ALTER INDEX [PK_OrchestrationInstances] ON [Runtime].[OrchestrationInstances] SET (OPTIMIZE_FOR_SEQUENTIAL_KEY = ON);');
        EXEC(N'ALTER INDEX [PK_StageExecutions] ON [Runtime].[StageExecutions] SET (OPTIMIZE_FOR_SEQUENTIAL_KEY = ON);');
        EXEC(N'ALTER INDEX [PK_TaskExecutions] ON [Runtime].[TaskExecutions] SET (OPTIMIZE_FOR_SEQUENTIAL_KEY = ON);');
        EXEC(N'ALTER INDEX [IX_TaskExecutions_Waiting_ResponseDeadlineUtc_Id] ON [Runtime].[TaskExecutions] SET (OPTIMIZE_FOR_SEQUENTIAL_KEY = ON);');
        EXEC(N'ALTER INDEX [PK_TaskDispatches] ON [Runtime].[TaskDispatches] SET (OPTIMIZE_FOR_SEQUENTIAL_KEY = ON);');
        EXEC(N'ALTER INDEX [PK_TaskExecutionAttempts] ON [Runtime].[TaskExecutionAttempts] SET (OPTIMIZE_FOR_SEQUENTIAL_KEY = ON);');
        EXEC(N'ALTER INDEX [CIX_TaskDispatches_StorageSequence] ON [Runtime].[TaskDispatches] SET (OPTIMIZE_FOR_SEQUENTIAL_KEY = ON);');
        EXEC(N'ALTER INDEX [CIX_TaskExecutionAttempts_StorageSequence] ON [Runtime].[TaskExecutionAttempts] SET (OPTIMIZE_FOR_SEQUENTIAL_KEY = ON);');
    END;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261124214346_EnablePortableSqlServerDatabaseSettings'
)
BEGIN
    INSERT INTO [Runtime].[__RuntimeStorageMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261124214346_EnablePortableSqlServerDatabaseSettings', N'10.0.10');
END;
GO

