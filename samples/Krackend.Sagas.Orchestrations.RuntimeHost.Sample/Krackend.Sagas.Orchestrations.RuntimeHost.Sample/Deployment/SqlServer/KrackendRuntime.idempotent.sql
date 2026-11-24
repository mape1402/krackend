SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET QUOTED_IDENTIFIER ON;
SET NUMERIC_ROUNDABORT OFF;
GO
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
    WHERE [MigrationId] = N'20260822200540_InitialRuntimeStorage'
)
BEGIN
    IF SCHEMA_ID(N'Runtime') IS NULL EXEC(N'CREATE SCHEMA [Runtime];');
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20260822200540_InitialRuntimeStorage'
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
    WHERE [MigrationId] = N'20260822200540_InitialRuntimeStorage'
)
BEGIN
    CREATE TABLE [Runtime].[EnvironmentVariableValues] (
        [Id] binary(16) NOT NULL,
        [EnvironmentKey] nvarchar(128) NOT NULL,
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
    WHERE [MigrationId] = N'20260822200540_InitialRuntimeStorage'
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
    WHERE [MigrationId] = N'20260822200540_InitialRuntimeStorage'
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
    WHERE [MigrationId] = N'20260822200540_InitialRuntimeStorage'
)
BEGIN
    CREATE TABLE [Runtime].[MuleActions] (
        [Id] uniqueidentifier NOT NULL,
        [Key] nvarchar(256) NOT NULL,
        [Lane] nvarchar(128) NOT NULL,
        [Payload] nvarchar(max) NOT NULL,
        [PayloadType] nvarchar(1024) NOT NULL,
        [Metadata] nvarchar(max) NULL,
        [CorrelationId] nvarchar(256) NULL,
        [DeduplicationKey] nvarchar(512) NULL,
        [Status] int NOT NULL,
        [Attempts] int NOT NULL,
        [LastError] nvarchar(max) NULL,
        [CreatedOnUtc] datetimeoffset NOT NULL,
        [LockedOnUtc] datetimeoffset NULL,
        [StartedOnUtc] datetimeoffset NULL,
        [NextAttemptOnUtc] datetimeoffset NULL,
        [CompletedOnUtc] datetimeoffset NULL,
        [TerminalOnUtc] datetimeoffset NULL,
        CONSTRAINT [PK_MuleActions] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20260822200540_InitialRuntimeStorage'
)
BEGIN
    CREATE TABLE [Runtime].[OrchestrationInstances] (
        [Id] binary(16) NOT NULL,
        [EnvironmentKey] nvarchar(128) NOT NULL,
        [OrchestrationDefinitionKey] nvarchar(256) NOT NULL,
        [RuntimeOrchestrationArtifactId] binary(16) NOT NULL,
        [TriggerIntakeId] binary(16) NOT NULL,
        [CorrelationId] nvarchar(256) NOT NULL,
        [ExecutionKey] nvarchar(512) NOT NULL,
        [Status] nvarchar(64) NOT NULL,
        [CurrentStageKey] nvarchar(256) NULL,
        [CurrentTaskKey] nvarchar(256) NULL,
        [CurrentParallelGroupKey] nvarchar(256) NULL,
        [StartedOnUtc] datetime2 NOT NULL,
        [LastUpdatedOnUtc] datetime2 NOT NULL,
        [WaitingSinceUtc] datetime2 NULL,
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
    WHERE [MigrationId] = N'20260822200540_InitialRuntimeStorage'
)
BEGIN
    CREATE TABLE [Runtime].[RuntimeIngressConfigurations] (
        [Id] binary(16) NOT NULL,
        [RuntimeOrchestrationArtifactId] binary(16) NOT NULL,
        [ConfigurationKey] nvarchar(256) NOT NULL,
        [IngressKind] nvarchar(64) NOT NULL,
        [IngressTransport] nvarchar(64) NOT NULL,
        [SettingsPayload] nvarchar(max) NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedOnUtc] datetime2 NOT NULL,
        [UpdatedOnUtc] datetime2 NOT NULL,
        [DeactivatedOnUtc] datetime2 NULL,
        CONSTRAINT [PK_RuntimeIngressConfigurations] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20260822200540_InitialRuntimeStorage'
)
BEGIN
    CREATE TABLE [Runtime].[RuntimeOrchestrationArtifacts] (
        [Id] binary(16) NOT NULL,
        [EnvironmentKey] nvarchar(128) NOT NULL,
        [OrchestrationDefinitionKey] nvarchar(256) NOT NULL,
        [ArtifactType] nvarchar(128) NOT NULL,
        [SourceOrchestrationVersionId] binary(16) NOT NULL,
        [Version] nvarchar(64) NOT NULL,
        [ArtifactChecksum] nvarchar(256) NOT NULL,
        [ArtifactPayload] nvarchar(max) NOT NULL,
        [IsActive] bit NOT NULL,
        [LoadedToCache] bit NOT NULL,
        [DeployedOnUtc] datetime2 NOT NULL,
        [ActivatedOnUtc] datetime2 NULL,
        [RetiredOnUtc] datetime2 NULL,
        [SupersededByArtifactId] binary(16) NULL,
        [Notes] nvarchar(2000) NULL,
        CONSTRAINT [PK_RuntimeOrchestrationArtifacts] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20260822200540_InitialRuntimeStorage'
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
    WHERE [MigrationId] = N'20260822200540_InitialRuntimeStorage'
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
        [ScheduledOnUtc] datetime2 NULL,
        [AcknowledgedOnUtc] datetime2 NULL,
        [FailedOnUtc] datetime2 NULL,
        [FailureReason] nvarchar(4000) NULL,
        [Metadata] nvarchar(max) NULL,
        CONSTRAINT [PK_TaskDispatches] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20260822200540_InitialRuntimeStorage'
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
        CONSTRAINT [PK_TaskExecutionAttempts] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20260822200540_InitialRuntimeStorage'
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
        [CompletedOnUtc] datetime2 NULL,
        [FailedOnUtc] datetime2 NULL,
        [TimedOutOnUtc] datetime2 NULL,
        [LastAttemptNumber] int NOT NULL,
        [OutputVariablesPayload] nvarchar(max) NULL,
        [CorrelationId] nvarchar(256) NULL,
        [Metadata] nvarchar(max) NULL,
        CONSTRAINT [PK_TaskExecutions] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20260822200540_InitialRuntimeStorage'
)
BEGIN
    CREATE INDEX [IX_CompensationExecutions_OrchestrationInstanceId] ON [Runtime].[CompensationExecutions] ([OrchestrationInstanceId]);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20260822200540_InitialRuntimeStorage'
)
BEGIN
    CREATE UNIQUE INDEX [IX_EnvironmentVariableValues_EnvironmentKey_VariableKey] ON [Runtime].[EnvironmentVariableValues] ([EnvironmentKey], [VariableKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20260822200540_InitialRuntimeStorage'
)
BEGIN
    CREATE INDEX [IX_ExecutionTransitions_OccurredOnUtc] ON [Runtime].[ExecutionTransitions] ([OccurredOnUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20260822200540_InitialRuntimeStorage'
)
BEGIN
    CREATE INDEX [IX_ExecutionTransitions_OrchestrationInstanceId_OccurredOnUtc] ON [Runtime].[ExecutionTransitions] ([OrchestrationInstanceId], [OccurredOnUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20260822200540_InitialRuntimeStorage'
)
BEGIN
    CREATE UNIQUE INDEX [IX_InstanceVariables_OrchestrationInstanceId_Key] ON [Runtime].[InstanceVariables] ([OrchestrationInstanceId], [Key]);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20260822200540_InitialRuntimeStorage'
)
BEGIN
    CREATE INDEX [IX_MuleActions_Lane_Status_NextAttemptOnUtc_CreatedOnUtc] ON [Runtime].[MuleActions] ([Lane], [Status], [NextAttemptOnUtc], [CreatedOnUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20260822200540_InitialRuntimeStorage'
)
BEGIN
    CREATE INDEX [IX_MuleActions_Status_CompletedOnUtc] ON [Runtime].[MuleActions] ([Status], [CompletedOnUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20260822200540_InitialRuntimeStorage'
)
BEGIN
    CREATE INDEX [IX_MuleActions_Status_LockedOnUtc] ON [Runtime].[MuleActions] ([Status], [LockedOnUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20260822200540_InitialRuntimeStorage'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_MuleActions_Key_DeduplicationKey] ON [Runtime].[MuleActions] ([Key], [DeduplicationKey]) WHERE [DeduplicationKey] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20260822200540_InitialRuntimeStorage'
)
BEGIN
    CREATE INDEX [IX_OrchestrationInstances_CorrelationId] ON [Runtime].[OrchestrationInstances] ([CorrelationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20260822200540_InitialRuntimeStorage'
)
BEGIN
    CREATE INDEX [IX_OrchestrationInstances_EnvironmentKey_LastUpdatedOnUtc] ON [Runtime].[OrchestrationInstances] ([EnvironmentKey], [LastUpdatedOnUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20260822200540_InitialRuntimeStorage'
)
BEGIN
    CREATE INDEX [IX_RuntimeIngressConfigurations_IsActive_IngressTransport_IngressKind] ON [Runtime].[RuntimeIngressConfigurations] ([IsActive], [IngressTransport], [IngressKind]);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20260822200540_InitialRuntimeStorage'
)
BEGIN
    CREATE INDEX [IX_RuntimeIngressConfigurations_IsActive_RuntimeOrchestrationArtifactId] ON [Runtime].[RuntimeIngressConfigurations] ([IsActive], [RuntimeOrchestrationArtifactId]);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20260822200540_InitialRuntimeStorage'
)
BEGIN
    CREATE UNIQUE INDEX [IX_RuntimeIngressConfigurations_RuntimeOrchestrationArtifactId_ConfigurationKey] ON [Runtime].[RuntimeIngressConfigurations] ([RuntimeOrchestrationArtifactId], [ConfigurationKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20260822200540_InitialRuntimeStorage'
)
BEGIN
    CREATE INDEX [IX_RuntimeOrchestrationArtifacts_EnvironmentKey_OrchestrationDefinitionKey_IsActive] ON [Runtime].[RuntimeOrchestrationArtifacts] ([EnvironmentKey], [OrchestrationDefinitionKey], [IsActive]);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20260822200540_InitialRuntimeStorage'
)
BEGIN
    CREATE UNIQUE INDEX [IX_RuntimeOrchestrationArtifacts_EnvironmentKey_OrchestrationDefinitionKey_Version] ON [Runtime].[RuntimeOrchestrationArtifacts] ([EnvironmentKey], [OrchestrationDefinitionKey], [Version]);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20260822200540_InitialRuntimeStorage'
)
BEGIN
    CREATE UNIQUE INDEX [IX_StageExecutions_OrchestrationInstanceId_StageKey] ON [Runtime].[StageExecutions] ([OrchestrationInstanceId], [StageKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20260822200540_InitialRuntimeStorage'
)
BEGIN
    CREATE INDEX [IX_TaskDispatches_CommandId] ON [Runtime].[TaskDispatches] ([CommandId]);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20260822200540_InitialRuntimeStorage'
)
BEGIN
    CREATE INDEX [IX_TaskDispatches_DispatchStatus] ON [Runtime].[TaskDispatches] ([DispatchStatus]);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20260822200540_InitialRuntimeStorage'
)
BEGIN
    CREATE UNIQUE INDEX [IX_TaskDispatches_TaskExecutionAttemptId] ON [Runtime].[TaskDispatches] ([TaskExecutionAttemptId]);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20260822200540_InitialRuntimeStorage'
)
BEGIN
    CREATE INDEX [IX_TaskExecutionAttempts_DispatchId] ON [Runtime].[TaskExecutionAttempts] ([DispatchId]);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20260822200540_InitialRuntimeStorage'
)
BEGIN
    CREATE INDEX [IX_TaskExecutionAttempts_Status] ON [Runtime].[TaskExecutionAttempts] ([Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20260822200540_InitialRuntimeStorage'
)
BEGIN
    CREATE UNIQUE INDEX [IX_TaskExecutionAttempts_TaskExecutionId_AttemptNumber] ON [Runtime].[TaskExecutionAttempts] ([TaskExecutionId], [AttemptNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20260822200540_InitialRuntimeStorage'
)
BEGIN
    CREATE INDEX [IX_TaskExecutions_CorrelationId] ON [Runtime].[TaskExecutions] ([CorrelationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20260822200540_InitialRuntimeStorage'
)
BEGIN
    CREATE UNIQUE INDEX [IX_TaskExecutions_StageExecutionId_TaskKey] ON [Runtime].[TaskExecutions] ([StageExecutionId], [TaskKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20260822200540_InitialRuntimeStorage'
)
BEGIN
    CREATE INDEX [IX_TaskExecutions_Status] ON [Runtime].[TaskExecutions] ([Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20260822200540_InitialRuntimeStorage'
)
BEGIN
    INSERT INTO [Runtime].[__RuntimeStorageMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260822200540_InitialRuntimeStorage', N'10.0.0');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20260824231700_AddRuntimeDesignNodes'
)
BEGIN
    CREATE TABLE [Runtime].[RuntimeDesignNodes] (
        [Id] binary(16) NOT NULL,
        [Key] nvarchar(128) NOT NULL,
        [Name] nvarchar(256) NOT NULL,
        [EndpointBaseUri] nvarchar(1024) NOT NULL,
        [RemoteRuntimeNodeId] nvarchar(128) NOT NULL,
        [ClientId] nvarchar(256) NOT NULL,
        [SecretReference] nvarchar(256) NOT NULL,
        [ProtectedSecret] nvarchar(max) NOT NULL,
        [Description] nvarchar(2000) NULL,
        [IsEnabled] bit NOT NULL,
        [CreatedOnUtc] datetime2 NOT NULL,
        [UpdatedOnUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_RuntimeDesignNodes] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20260824231700_AddRuntimeDesignNodes'
)
BEGIN
    CREATE UNIQUE INDEX [IX_RuntimeDesignNodes_ClientId] ON [Runtime].[RuntimeDesignNodes] ([ClientId]);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20260824231700_AddRuntimeDesignNodes'
)
BEGIN
    CREATE INDEX [IX_RuntimeDesignNodes_IsEnabled] ON [Runtime].[RuntimeDesignNodes] ([IsEnabled]);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20260824231700_AddRuntimeDesignNodes'
)
BEGIN
    CREATE UNIQUE INDEX [IX_RuntimeDesignNodes_Key] ON [Runtime].[RuntimeDesignNodes] ([Key]);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20260824231700_AddRuntimeDesignNodes'
)
BEGIN
    INSERT INTO [Runtime].[__RuntimeStorageMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260824231700_AddRuntimeDesignNodes', N'10.0.0');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20260825074723_AddRuntimeArtifactLifecycle'
)
BEGIN
    ALTER TABLE [Runtime].[RuntimeOrchestrationArtifacts] ADD [IngressGeneration] bigint NOT NULL DEFAULT CAST(0 AS bigint);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20260825074723_AddRuntimeArtifactLifecycle'
)
BEGIN
    ALTER TABLE [Runtime].[RuntimeOrchestrationArtifacts] ADD [ProjectionCompletedOnUtc] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20260825074723_AddRuntimeArtifactLifecycle'
)
BEGIN
    ALTER TABLE [Runtime].[RuntimeOrchestrationArtifacts] ADD [ProjectionError] nvarchar(4000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20260825074723_AddRuntimeArtifactLifecycle'
)
BEGIN
    ALTER TABLE [Runtime].[RuntimeOrchestrationArtifacts] ADD [ProjectionFailedOnUtc] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20260825074723_AddRuntimeArtifactLifecycle'
)
BEGIN
    ALTER TABLE [Runtime].[RuntimeOrchestrationArtifacts] ADD [ProjectionStartedOnUtc] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20260825074723_AddRuntimeArtifactLifecycle'
)
BEGIN
    ALTER TABLE [Runtime].[RuntimeOrchestrationArtifacts] ADD [Status] nvarchar(64) NOT NULL DEFAULT N'Pending';
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20260825074723_AddRuntimeArtifactLifecycle'
)
BEGIN
    EXEC(N'
        UPDATE [Runtime].[RuntimeOrchestrationArtifacts]
        SET [IngressGeneration] = 1,
            [Status] = CASE WHEN [IsActive] = 1 THEN N''Ready'' ELSE N''Retired'' END,
            [ProjectionCompletedOnUtc] = CASE WHEN [IsActive] = 1 THEN COALESCE([ActivatedOnUtc], [DeployedOnUtc]) ELSE NULL END,
            [ActivatedOnUtc] = CASE WHEN [IsActive] = 1 THEN COALESCE([ActivatedOnUtc], [DeployedOnUtc]) ELSE [ActivatedOnUtc] END
    ')
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20260825074723_AddRuntimeArtifactLifecycle'
)
BEGIN
    CREATE INDEX [IX_RuntimeOrchestrationArtifacts_EnvironmentKey_Status_IsActive] ON [Runtime].[RuntimeOrchestrationArtifacts] ([EnvironmentKey], [Status], [IsActive]);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20260825074723_AddRuntimeArtifactLifecycle'
)
BEGIN
    INSERT INTO [Runtime].[__RuntimeStorageMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260825074723_AddRuntimeArtifactLifecycle', N'10.0.0');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20260825194038_AddRuntimeDesignNodeConnectionTokens'
)
BEGIN
    DROP INDEX [IX_RuntimeDesignNodes_ClientId] ON [Runtime].[RuntimeDesignNodes];
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20260825194038_AddRuntimeDesignNodeConnectionTokens'
)
BEGIN
    DECLARE @var nvarchar(max);
    SELECT @var = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Runtime].[RuntimeDesignNodes]') AND [c].[name] = N'ClientId');
    IF @var IS NOT NULL EXEC(N'ALTER TABLE [Runtime].[RuntimeDesignNodes] DROP CONSTRAINT ' + @var + ';');
    ALTER TABLE [Runtime].[RuntimeDesignNodes] DROP COLUMN [ClientId];
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20260825194038_AddRuntimeDesignNodeConnectionTokens'
)
BEGIN
    DECLARE @var1 nvarchar(max);
    SELECT @var1 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Runtime].[RuntimeDesignNodes]') AND [c].[name] = N'ProtectedSecret');
    IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [Runtime].[RuntimeDesignNodes] DROP CONSTRAINT ' + @var1 + ';');
    ALTER TABLE [Runtime].[RuntimeDesignNodes] DROP COLUMN [ProtectedSecret];
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20260825194038_AddRuntimeDesignNodeConnectionTokens'
)
BEGIN
    DECLARE @var2 nvarchar(max);
    SELECT @var2 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Runtime].[RuntimeDesignNodes]') AND [c].[name] = N'SecretReference');
    IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [Runtime].[RuntimeDesignNodes] DROP CONSTRAINT ' + @var2 + ';');
    ALTER TABLE [Runtime].[RuntimeDesignNodes] DROP COLUMN [SecretReference];
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20260825194038_AddRuntimeDesignNodeConnectionTokens'
)
BEGIN
    ALTER TABLE [Runtime].[RuntimeDesignNodes] ADD [AccessTokenTtlSeconds] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20260825194038_AddRuntimeDesignNodeConnectionTokens'
)
BEGIN
    ALTER TABLE [Runtime].[RuntimeDesignNodes] ADD [DistributionMode] nvarchar(64) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20260825194038_AddRuntimeDesignNodeConnectionTokens'
)
BEGIN
    ALTER TABLE [Runtime].[RuntimeDesignNodes] ADD [InboundAllowedScopes] nvarchar(512) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20260825194038_AddRuntimeDesignNodeConnectionTokens'
)
BEGIN
    ALTER TABLE [Runtime].[RuntimeDesignNodes] ADD [InboundClientId] nvarchar(256) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20260825194038_AddRuntimeDesignNodeConnectionTokens'
)
BEGIN
    ALTER TABLE [Runtime].[RuntimeDesignNodes] ADD [InboundCredentialCreatedAtUtc] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20260825194038_AddRuntimeDesignNodeConnectionTokens'
)
BEGIN
    ALTER TABLE [Runtime].[RuntimeDesignNodes] ADD [InboundCredentialRevokedAtUtc] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20260825194038_AddRuntimeDesignNodeConnectionTokens'
)
BEGIN
    ALTER TABLE [Runtime].[RuntimeDesignNodes] ADD [InboundCredentialRotatedAtUtc] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20260825194038_AddRuntimeDesignNodeConnectionTokens'
)
BEGIN
    ALTER TABLE [Runtime].[RuntimeDesignNodes] ADD [InboundCredentialStatus] nvarchar(64) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20260825194038_AddRuntimeDesignNodeConnectionTokens'
)
BEGIN
    ALTER TABLE [Runtime].[RuntimeDesignNodes] ADD [InboundKeyId] nvarchar(256) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20260825194038_AddRuntimeDesignNodeConnectionTokens'
)
BEGIN
    ALTER TABLE [Runtime].[RuntimeDesignNodes] ADD [InboundLastFailureReason] nvarchar(2000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20260825194038_AddRuntimeDesignNodeConnectionTokens'
)
BEGIN
    ALTER TABLE [Runtime].[RuntimeDesignNodes] ADD [InboundLastTokenFailedAtUtc] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20260825194038_AddRuntimeDesignNodeConnectionTokens'
)
BEGIN
    ALTER TABLE [Runtime].[RuntimeDesignNodes] ADD [InboundLastTokenIssuedAtUtc] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20260825194038_AddRuntimeDesignNodeConnectionTokens'
)
BEGIN
    ALTER TABLE [Runtime].[RuntimeDesignNodes] ADD [InboundSecretHash] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20260825194038_AddRuntimeDesignNodeConnectionTokens'
)
BEGIN
    ALTER TABLE [Runtime].[RuntimeDesignNodes] ADD [OutboundClientId] nvarchar(256) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20260825194038_AddRuntimeDesignNodeConnectionTokens'
)
BEGIN
    ALTER TABLE [Runtime].[RuntimeDesignNodes] ADD [OutboundCredentialImportedAtUtc] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20260825194038_AddRuntimeDesignNodeConnectionTokens'
)
BEGIN
    ALTER TABLE [Runtime].[RuntimeDesignNodes] ADD [OutboundCredentialStatus] nvarchar(64) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20260825194038_AddRuntimeDesignNodeConnectionTokens'
)
BEGIN
    ALTER TABLE [Runtime].[RuntimeDesignNodes] ADD [OutboundKeyId] nvarchar(256) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20260825194038_AddRuntimeDesignNodeConnectionTokens'
)
BEGIN
    ALTER TABLE [Runtime].[RuntimeDesignNodes] ADD [OutboundLastTokenReceivedAtUtc] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20260825194038_AddRuntimeDesignNodeConnectionTokens'
)
BEGIN
    ALTER TABLE [Runtime].[RuntimeDesignNodes] ADD [OutboundRequestedScopes] nvarchar(512) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20260825194038_AddRuntimeDesignNodeConnectionTokens'
)
BEGIN
    ALTER TABLE [Runtime].[RuntimeDesignNodes] ADD [ProtectedOutboundSecret] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20260825194038_AddRuntimeDesignNodeConnectionTokens'
)
BEGIN
    ALTER TABLE [Runtime].[RuntimeDesignNodes] ADD [TokenRefreshSkewSeconds] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20260825194038_AddRuntimeDesignNodeConnectionTokens'
)
BEGIN
    ALTER TABLE [Runtime].[RuntimeDesignNodes] ADD [TokenValidationCacheTtlSeconds] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20260825194038_AddRuntimeDesignNodeConnectionTokens'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_RuntimeDesignNodes_InboundClientId] ON [Runtime].[RuntimeDesignNodes] ([InboundClientId]) WHERE [InboundClientId] IS NOT NULL AND [InboundClientId] <> ''''');
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20260825194038_AddRuntimeDesignNodeConnectionTokens'
)
BEGIN
    INSERT INTO [Runtime].[__RuntimeStorageMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260825194038_AddRuntimeDesignNodeConnectionTokens', N'10.0.0');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20260826212311_AddRuntimeDesignNodeLifecycleStatus'
)
BEGIN
    ALTER TABLE [Runtime].[RuntimeDesignNodes] ADD [Status] nvarchar(64) NOT NULL DEFAULT N'Pending';
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20260826212311_AddRuntimeDesignNodeLifecycleStatus'
)
BEGIN
    EXEC(N'
        UPDATE [Runtime].[RuntimeDesignNodes]
        SET [Status] = CASE
            WHEN [IsEnabled] = CAST(1 AS bit) THEN N''Enabled''
            ELSE N''Suspend''
        END
    ')
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20260826212311_AddRuntimeDesignNodeLifecycleStatus'
)
BEGIN
    CREATE INDEX [IX_RuntimeDesignNodes_Status] ON [Runtime].[RuntimeDesignNodes] ([Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20260826212311_AddRuntimeDesignNodeLifecycleStatus'
)
BEGIN
    INSERT INTO [Runtime].[__RuntimeStorageMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260826212311_AddRuntimeDesignNodeLifecycleStatus', N'10.0.0');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20260908230749_RemoveRuntimeEnvironmentDimension'
)
BEGIN
    DROP INDEX [IX_RuntimeOrchestrationArtifacts_EnvironmentKey_OrchestrationDefinitionKey_IsActive] ON [Runtime].[RuntimeOrchestrationArtifacts];
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20260908230749_RemoveRuntimeEnvironmentDimension'
)
BEGIN
    DROP INDEX [IX_RuntimeOrchestrationArtifacts_EnvironmentKey_OrchestrationDefinitionKey_Version] ON [Runtime].[RuntimeOrchestrationArtifacts];
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20260908230749_RemoveRuntimeEnvironmentDimension'
)
BEGIN
    DROP INDEX [IX_RuntimeOrchestrationArtifacts_EnvironmentKey_Status_IsActive] ON [Runtime].[RuntimeOrchestrationArtifacts];
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20260908230749_RemoveRuntimeEnvironmentDimension'
)
BEGIN
    DROP INDEX [IX_OrchestrationInstances_EnvironmentKey_LastUpdatedOnUtc] ON [Runtime].[OrchestrationInstances];
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20260908230749_RemoveRuntimeEnvironmentDimension'
)
BEGIN
    DROP INDEX [IX_EnvironmentVariableValues_EnvironmentKey_VariableKey] ON [Runtime].[EnvironmentVariableValues];
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20260908230749_RemoveRuntimeEnvironmentDimension'
)
BEGIN
    DECLARE @var3 nvarchar(max);
    SELECT @var3 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Runtime].[RuntimeOrchestrationArtifacts]') AND [c].[name] = N'EnvironmentKey');
    IF @var3 IS NOT NULL EXEC(N'ALTER TABLE [Runtime].[RuntimeOrchestrationArtifacts] DROP CONSTRAINT ' + @var3 + ';');
    ALTER TABLE [Runtime].[RuntimeOrchestrationArtifacts] DROP COLUMN [EnvironmentKey];
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20260908230749_RemoveRuntimeEnvironmentDimension'
)
BEGIN
    DECLARE @var4 nvarchar(max);
    SELECT @var4 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Runtime].[OrchestrationInstances]') AND [c].[name] = N'EnvironmentKey');
    IF @var4 IS NOT NULL EXEC(N'ALTER TABLE [Runtime].[OrchestrationInstances] DROP CONSTRAINT ' + @var4 + ';');
    ALTER TABLE [Runtime].[OrchestrationInstances] DROP COLUMN [EnvironmentKey];
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20260908230749_RemoveRuntimeEnvironmentDimension'
)
BEGIN
    DECLARE @var5 nvarchar(max);
    SELECT @var5 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Runtime].[EnvironmentVariableValues]') AND [c].[name] = N'EnvironmentKey');
    IF @var5 IS NOT NULL EXEC(N'ALTER TABLE [Runtime].[EnvironmentVariableValues] DROP CONSTRAINT ' + @var5 + ';');
    ALTER TABLE [Runtime].[EnvironmentVariableValues] DROP COLUMN [EnvironmentKey];
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20260908230749_RemoveRuntimeEnvironmentDimension'
)
BEGIN
    CREATE INDEX [IX_RuntimeOrchestrationArtifacts_OrchestrationDefinitionKey_IsActive] ON [Runtime].[RuntimeOrchestrationArtifacts] ([OrchestrationDefinitionKey], [IsActive]);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20260908230749_RemoveRuntimeEnvironmentDimension'
)
BEGIN
    CREATE UNIQUE INDEX [IX_RuntimeOrchestrationArtifacts_OrchestrationDefinitionKey_Version] ON [Runtime].[RuntimeOrchestrationArtifacts] ([OrchestrationDefinitionKey], [Version]);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20260908230749_RemoveRuntimeEnvironmentDimension'
)
BEGIN
    CREATE INDEX [IX_RuntimeOrchestrationArtifacts_Status_IsActive] ON [Runtime].[RuntimeOrchestrationArtifacts] ([Status], [IsActive]);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20260908230749_RemoveRuntimeEnvironmentDimension'
)
BEGIN
    CREATE INDEX [IX_OrchestrationInstances_LastUpdatedOnUtc] ON [Runtime].[OrchestrationInstances] ([LastUpdatedOnUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20260908230749_RemoveRuntimeEnvironmentDimension'
)
BEGIN
    CREATE UNIQUE INDEX [IX_EnvironmentVariableValues_VariableKey] ON [Runtime].[EnvironmentVariableValues] ([VariableKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20260908230749_RemoveRuntimeEnvironmentDimension'
)
BEGIN
    INSERT INTO [Runtime].[__RuntimeStorageMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260908230749_RemoveRuntimeEnvironmentDimension', N'10.0.0');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20260910085029_AddOrchestrationInstanceSagaId'
)
BEGIN
    ALTER TABLE [Runtime].[OrchestrationInstances] ADD [SagaId] nvarchar(256) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20260910085029_AddOrchestrationInstanceSagaId'
)
BEGIN
    EXEC(N'UPDATE [Runtime].[OrchestrationInstances] SET [SagaId] = [CorrelationId] WHERE [SagaId] = '''' OR [SagaId] IS NULL')
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20260910085029_AddOrchestrationInstanceSagaId'
)
BEGIN
    CREATE INDEX [IX_OrchestrationInstances_SagaId] ON [Runtime].[OrchestrationInstances] ([SagaId]);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20260910085029_AddOrchestrationInstanceSagaId'
)
BEGIN
    INSERT INTO [Runtime].[__RuntimeStorageMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260910085029_AddOrchestrationInstanceSagaId', N'10.0.0');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261001170152_AddRuntimeDataProtectionKeys'
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
    WHERE [MigrationId] = N'20261001170152_AddRuntimeDataProtectionKeys'
)
BEGIN
    INSERT INTO [Runtime].[__RuntimeStorageMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261001170152_AddRuntimeDataProtectionKeys', N'10.0.0');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261001204118_AddOrchestrationInstanceStartIdempotency'
)
BEGIN
    ALTER TABLE [Runtime].[OrchestrationInstances] ADD [StartIdempotencyKey] nvarchar(512) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261001204118_AddOrchestrationInstanceStartIdempotency'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_OrchestrationInstances_StartIdempotencyKey] ON [Runtime].[OrchestrationInstances] ([StartIdempotencyKey]) WHERE [StartIdempotencyKey] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261001204118_AddOrchestrationInstanceStartIdempotency'
)
BEGIN
    INSERT INTO [Runtime].[__RuntimeStorageMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261001204118_AddOrchestrationInstanceStartIdempotency', N'10.0.0');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261002220845_AddRuntimeExtensionPackages'
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
    WHERE [MigrationId] = N'20261002220845_AddRuntimeExtensionPackages'
)
BEGIN
    CREATE UNIQUE INDEX [IX_RuntimeExtensionPackages_BundleId_ExtensionKey_Version] ON [Runtime].[RuntimeExtensionPackages] ([BundleId], [ExtensionKey], [Version]);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261002220845_AddRuntimeExtensionPackages'
)
BEGIN
    CREATE INDEX [IX_RuntimeExtensionPackages_ExtensionKey_Version_Status] ON [Runtime].[RuntimeExtensionPackages] ([ExtensionKey], [Version], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261002220845_AddRuntimeExtensionPackages'
)
BEGIN
    INSERT INTO [Runtime].[__RuntimeStorageMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261002220845_AddRuntimeExtensionPackages', N'10.0.0');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261006120000_AddCompensationExecutionDeduplication'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_CompensationExecutions_Instance_SourceTask_CompensationTask] ON [Runtime].[CompensationExecutions] ([OrchestrationInstanceId], [SourceTaskExecutionId], [CompensationTaskKey]) WHERE [OrchestrationInstanceId] IS NOT NULL AND [SourceTaskExecutionId] IS NOT NULL AND [CompensationTaskKey] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261006120000_AddCompensationExecutionDeduplication'
)
BEGIN
    INSERT INTO [Runtime].[__RuntimeStorageMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261006120000_AddCompensationExecutionDeduplication', N'10.0.0');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261018042406_AddRuntimeArtifactLifecycleAndTriggerClaims'
)
BEGIN
    ALTER TABLE [Runtime].[RuntimeOrchestrationArtifacts] ADD [LastControlPlaneArtifactId] nvarchar(128) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261018042406_AddRuntimeArtifactLifecycleAndTriggerClaims'
)
BEGIN
    ALTER TABLE [Runtime].[RuntimeOrchestrationArtifacts] ADD [LastReleaseId] nvarchar(128) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261018042406_AddRuntimeArtifactLifecycleAndTriggerClaims'
)
BEGIN
    ALTER TABLE [Runtime].[RuntimeOrchestrationArtifacts] ADD [LifecycleStatus] nvarchar(64) NOT NULL DEFAULT N'Active';
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261018042406_AddRuntimeArtifactLifecycleAndTriggerClaims'
)
BEGIN
    ALTER TABLE [Runtime].[RuntimeOrchestrationArtifacts] ADD [ReleaseStrategy] nvarchar(64) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261018042406_AddRuntimeArtifactLifecycleAndTriggerClaims'
)
BEGIN
    ALTER TABLE [Runtime].[RuntimeIngressConfigurations] ADD [MessageDisposition] nvarchar(64) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261018042406_AddRuntimeArtifactLifecycleAndTriggerClaims'
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
    WHERE [MigrationId] = N'20261018042406_AddRuntimeArtifactLifecycleAndTriggerClaims'
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
    WHERE [MigrationId] = N'20261018042406_AddRuntimeArtifactLifecycleAndTriggerClaims'
)
BEGIN
    CREATE INDEX [IX_RuntimeOrchestrationArtifacts_LifecycleStatus_IsActive] ON [Runtime].[RuntimeOrchestrationArtifacts] ([LifecycleStatus], [IsActive]);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261018042406_AddRuntimeArtifactLifecycleAndTriggerClaims'
)
BEGIN
    CREATE UNIQUE INDEX [IX_RuntimeArtifactDeploymentHistory_ControlPlaneArtifactId] ON [Runtime].[RuntimeArtifactDeploymentHistory] ([ControlPlaneArtifactId]);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261018042406_AddRuntimeArtifactLifecycleAndTriggerClaims'
)
BEGIN
    CREATE INDEX [IX_RuntimeArtifactDeploymentHistory_OrchestrationDefinitionKey_AppliedOnUtc] ON [Runtime].[RuntimeArtifactDeploymentHistory] ([OrchestrationDefinitionKey], [AppliedOnUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261018042406_AddRuntimeArtifactLifecycleAndTriggerClaims'
)
BEGIN
    CREATE INDEX [IX_RuntimeArtifactDeploymentHistory_ReleaseId] ON [Runtime].[RuntimeArtifactDeploymentHistory] ([ReleaseId]);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261018042406_AddRuntimeArtifactLifecycleAndTriggerClaims'
)
BEGIN
    CREATE INDEX [IX_RuntimeTriggerClaims_RuntimeOrchestrationArtifactId] ON [Runtime].[RuntimeTriggerClaims] ([RuntimeOrchestrationArtifactId]);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261018042406_AddRuntimeArtifactLifecycleAndTriggerClaims'
)
BEGIN
    INSERT INTO [Runtime].[__RuntimeStorageMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261018042406_AddRuntimeArtifactLifecycleAndTriggerClaims', N'10.0.0');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261024061405_UpgradeMuleToV21Replay'
)
BEGIN
    ALTER TABLE [Runtime].[MuleActions] DROP CONSTRAINT [PK_MuleActions];
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261024061405_UpgradeMuleToV21Replay'
)
BEGIN
    DROP INDEX [UX_MuleActions_Key_DeduplicationKey] ON [Runtime].[MuleActions];
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261024061405_UpgradeMuleToV21Replay'
)
BEGIN
    EXEC sp_rename N'[Runtime].[MuleActions]', N'DurableAction', 'OBJECT';
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261024061405_UpgradeMuleToV21Replay'
)
BEGIN
    EXEC sp_rename N'[Runtime].[DurableAction].[IX_MuleActions_Status_LockedOnUtc]', N'IX_DurableAction_Status_LockedOnUtc', 'INDEX';
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261024061405_UpgradeMuleToV21Replay'
)
BEGIN
    EXEC sp_rename N'[Runtime].[DurableAction].[IX_MuleActions_Status_CompletedOnUtc]', N'IX_DurableAction_Status_CompletedOnUtc', 'INDEX';
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261024061405_UpgradeMuleToV21Replay'
)
BEGIN
    EXEC sp_rename N'[Runtime].[DurableAction].[IX_MuleActions_Lane_Status_NextAttemptOnUtc_CreatedOnUtc]', N'IX_DurableAction_Lane_Status_NextAttemptOnUtc_CreatedOnUtc', 'INDEX';
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261024061405_UpgradeMuleToV21Replay'
)
BEGIN
    ALTER TABLE [Runtime].[DurableAction] ADD [MuleV2Id] nvarchar(26) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261024061405_UpgradeMuleToV21Replay'
)
BEGIN
    ALTER TABLE [Runtime].[DurableAction] ADD [MuleV2ConcurrencyToken] nvarchar(26) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261024061405_UpgradeMuleToV21Replay'
)
BEGIN
    EXEC(N'
                    CREATE FUNCTION [Runtime].[ToMuleV2Ulid]
                    (
                        @legacyId uniqueidentifier,
                        @createdOnUtc datetimeoffset
                    )
                    RETURNS varchar(26)
                    AS
                    BEGIN
                        DECLARE @alphabet varchar(32) = ''0123456789ABCDEFGHJKMNPQRSTVWXYZ'';
                        DECLARE @timestamp bigint = DATEDIFF_BIG(
                            millisecond,
                            CONVERT(datetimeoffset, ''1970-01-01T00:00:00+00:00''),
                            SWITCHOFFSET(@createdOnUtc, ''+00:00''));
                        DECLARE @timestampPart varchar(10) = '''';
                        DECLARE @randomnessPart varchar(16) = '''';
                        DECLARE @position int = 10;
                        DECLARE @bytes varbinary(16) = CONVERT(varbinary(16), @legacyId);
                        DECLARE @bytePosition int = 1;
                        DECLARE @buffer bigint = 0;
                        DECLARE @bits int = 0;

                        IF @timestamp < 0
                            SET @timestamp = 0;

                        WHILE @position > 0
                        BEGIN
                            SET @timestampPart = SUBSTRING(@alphabet, (@timestamp % 32) + 1, 1) + @timestampPart;
                            SET @timestamp = @timestamp / 32;
                            SET @position = @position - 1;
                        END;

                        WHILE @bytePosition <= 16
                        BEGIN
                            SET @buffer = (@buffer * 256) + CONVERT(int, SUBSTRING(@bytes, @bytePosition, 1));
                            SET @bits = @bits + 8;

                            WHILE @bits >= 5
                            BEGIN
                                SET @bits = @bits - 5;
                                SET @randomnessPart = @randomnessPart + SUBSTRING(
                                    @alphabet,
                                    ((@buffer / POWER(CAST(2 AS bigint), @bits)) % 32) + 1,
                                    1);
                                SET @buffer = @buffer % POWER(CAST(2 AS bigint), @bits);
                            END;

                            SET @bytePosition = @bytePosition + 1;
                        END;

                        RETURN @timestampPart + @randomnessPart;
                    END;
                    ')
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261024061405_UpgradeMuleToV21Replay'
)
BEGIN

                    EXEC(N'
                        UPDATE [Runtime].[DurableAction]
                        SET [MuleV2Id] = [Runtime].[ToMuleV2Ulid]([Id], [CreatedOnUtc]),
                            [MuleV2ConcurrencyToken] = [Runtime].[ToMuleV2Ulid](NEWID(), [CreatedOnUtc]);
                    ')
                    
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261024061405_UpgradeMuleToV21Replay'
)
BEGIN
    DECLARE @var6 nvarchar(max);
    SELECT @var6 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Runtime].[DurableAction]') AND [c].[name] = N'Id');
    IF @var6 IS NOT NULL EXEC(N'ALTER TABLE [Runtime].[DurableAction] DROP CONSTRAINT ' + @var6 + ';');
    ALTER TABLE [Runtime].[DurableAction] DROP COLUMN [Id];
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261024061405_UpgradeMuleToV21Replay'
)
BEGIN
    EXEC sp_rename N'[Runtime].[DurableAction].[MuleV2Id]', N'Id', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261024061405_UpgradeMuleToV21Replay'
)
BEGIN
    EXEC sp_rename N'[Runtime].[DurableAction].[MuleV2ConcurrencyToken]', N'ConcurrencyToken', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261024061405_UpgradeMuleToV21Replay'
)
BEGIN
    DECLARE @var7 nvarchar(max);
    SELECT @var7 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Runtime].[DurableAction]') AND [c].[name] = N'Id');
    IF @var7 IS NOT NULL EXEC(N'ALTER TABLE [Runtime].[DurableAction] DROP CONSTRAINT ' + @var7 + ';');
    ALTER TABLE [Runtime].[DurableAction] ALTER COLUMN [Id] nvarchar(26) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261024061405_UpgradeMuleToV21Replay'
)
BEGIN
    DECLARE @var8 nvarchar(max);
    SELECT @var8 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Runtime].[DurableAction]') AND [c].[name] = N'ConcurrencyToken');
    IF @var8 IS NOT NULL EXEC(N'ALTER TABLE [Runtime].[DurableAction] DROP CONSTRAINT ' + @var8 + ';');
    ALTER TABLE [Runtime].[DurableAction] ALTER COLUMN [ConcurrencyToken] nvarchar(26) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261024061405_UpgradeMuleToV21Replay'
)
BEGIN
    ALTER TABLE [Runtime].[DurableAction] ADD CONSTRAINT [PK_DurableAction] PRIMARY KEY ([Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261024061405_UpgradeMuleToV21Replay'
)
BEGIN
    DROP FUNCTION [Runtime].[ToMuleV2Ulid];
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261024061405_UpgradeMuleToV21Replay'
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
        CONSTRAINT [PK_MuleReplay] PRIMARY KEY ([RequestId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261024061405_UpgradeMuleToV21Replay'
)
BEGIN
    CREATE INDEX [IX_DurableAction_Key_DeduplicationKey] ON [Runtime].[DurableAction] ([Key], [DeduplicationKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261024061405_UpgradeMuleToV21Replay'
)
BEGIN
    CREATE UNIQUE INDEX [IX_MuleReplay_ReplayActionId] ON [Runtime].[MuleReplay] ([ReplayActionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261024061405_UpgradeMuleToV21Replay'
)
BEGIN
    CREATE UNIQUE INDEX [IX_MuleReplay_SourceActionId_BranchId] ON [Runtime].[MuleReplay] ([SourceActionId], [BranchId]);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261024061405_UpgradeMuleToV21Replay'
)
BEGIN
    CREATE INDEX [IX_MuleReplay_Status_LockedOnUtc] ON [Runtime].[MuleReplay] ([Status], [LockedOnUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261024061405_UpgradeMuleToV21Replay'
)
BEGIN
    CREATE INDEX [IX_MuleReplay_Status_ScheduleOnUtc_NextAttemptOnUtc] ON [Runtime].[MuleReplay] ([Status], [ScheduleOnUtc], [NextAttemptOnUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261024061405_UpgradeMuleToV21Replay'
)
BEGIN
    INSERT INTO [Runtime].[__RuntimeStorageMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261024061405_UpgradeMuleToV21Replay', N'10.0.0');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261025022209_UpgradeMuleToV22Cancellation'
)
BEGIN
    ALTER TABLE [Runtime].[TaskDispatches] ADD [PhysicalCancellationAttemptCount] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261025022209_UpgradeMuleToV22Cancellation'
)
BEGIN
    ALTER TABLE [Runtime].[TaskDispatches] ADD [PhysicalCancellationCompletedOnUtc] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261025022209_UpgradeMuleToV22Cancellation'
)
BEGIN
    ALTER TABLE [Runtime].[TaskDispatches] ADD [PhysicalCancellationLastAttemptOnUtc] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261025022209_UpgradeMuleToV22Cancellation'
)
BEGIN
    ALTER TABLE [Runtime].[TaskDispatches] ADD [PhysicalCancellationPending] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261025022209_UpgradeMuleToV22Cancellation'
)
BEGIN
    ALTER TABLE [Runtime].[MuleReplay] ADD [CancelledOnUtc] datetimeoffset NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261025022209_UpgradeMuleToV22Cancellation'
)
BEGIN
    ALTER TABLE [Runtime].[DurableAction] ADD [CancelledOnUtc] datetimeoffset NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261025022209_UpgradeMuleToV22Cancellation'
)
BEGIN
    CREATE INDEX [IX_TaskDispatches_PhysicalCancellationPending] ON [Runtime].[TaskDispatches] ([PhysicalCancellationPending]);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261025022209_UpgradeMuleToV22Cancellation'
)
BEGIN
    INSERT INTO [Runtime].[__RuntimeStorageMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261025022209_UpgradeMuleToV22Cancellation', N'10.0.0');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261025022914_PersistDispatchCancellationIntent'
)
BEGIN
    ALTER TABLE [Runtime].[OrchestrationInstances] ADD [DispatchCancellationPending] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261025022914_PersistDispatchCancellationIntent'
)
BEGIN
    INSERT INTO [Runtime].[__RuntimeStorageMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261025022914_PersistDispatchCancellationIntent', N'10.0.0');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261025023146_IndexDispatchCancellationIntents'
)
BEGIN
    CREATE INDEX [IX_OrchestrationInstances_DispatchCancellationPending_LastUpdatedOnUtc] ON [Runtime].[OrchestrationInstances] ([DispatchCancellationPending], [LastUpdatedOnUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261025023146_IndexDispatchCancellationIntents'
)
BEGIN
    INSERT INTO [Runtime].[__RuntimeStorageMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261025023146_IndexDispatchCancellationIntents', N'10.0.0');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261026060446_IndexRuntimeInstanceMonitor'
)
BEGIN
    CREATE INDEX [IX_OrchestrationInstances_StartedOnUtc_Status] ON [Runtime].[OrchestrationInstances] ([StartedOnUtc], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261026060446_IndexRuntimeInstanceMonitor'
)
BEGIN
    CREATE INDEX [IX_OrchestrationInstances_Status_LastUpdatedOnUtc] ON [Runtime].[OrchestrationInstances] ([Status], [LastUpdatedOnUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261026060446_IndexRuntimeInstanceMonitor'
)
BEGIN
    INSERT INTO [Runtime].[__RuntimeStorageMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261026060446_IndexRuntimeInstanceMonitor', N'10.0.0');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261026062741_AddSagaStatisticsProjection'
)
BEGIN
    ALTER TABLE [Runtime].[OrchestrationInstances] ADD [StatisticsRevision] bigint NOT NULL DEFAULT CAST(0 AS bigint);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261026062741_AddSagaStatisticsProjection'
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
    WHERE [MigrationId] = N'20261026062741_AddSagaStatisticsProjection'
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
    WHERE [MigrationId] = N'20261026062741_AddSagaStatisticsProjection'
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
    WHERE [MigrationId] = N'20261026062741_AddSagaStatisticsProjection'
)
BEGIN
    CREATE INDEX [IX_OrchestrationInstances_StatisticsRevision] ON [Runtime].[OrchestrationInstances] ([StatisticsRevision]);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261026062741_AddSagaStatisticsProjection'
)
BEGIN
    CREATE UNIQUE INDEX [IX_SagaStatisticsObservations_InstanceId_Revision] ON [Runtime].[SagaStatisticsObservations] ([InstanceId], [Revision]);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261026062741_AddSagaStatisticsProjection'
)
BEGIN
    CREATE INDEX [IX_SagaStatisticsObservations_OrchestrationKey_OccurredOnUtc] ON [Runtime].[SagaStatisticsObservations] ([OrchestrationKey], [OccurredOnUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261026062741_AddSagaStatisticsProjection'
)
BEGIN
    CREATE INDEX [IX_SagaStatisticsObservations_Processed_InstanceId_Revision] ON [Runtime].[SagaStatisticsObservations] ([Processed], [InstanceId], [Revision]);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261026062741_AddSagaStatisticsProjection'
)
BEGIN
    CREATE INDEX [IX_SagaStatisticsProjections_OrchestrationKey_StartedOnUtc] ON [Runtime].[SagaStatisticsProjections] ([OrchestrationKey], [StartedOnUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261026062741_AddSagaStatisticsProjection'
)
BEGIN
    CREATE INDEX [IX_SagaStatisticsProjections_Status_StartedOnUtc] ON [Runtime].[SagaStatisticsProjections] ([Status], [StartedOnUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261026062741_AddSagaStatisticsProjection'
)
BEGIN
    INSERT INTO [Runtime].[__RuntimeStorageMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261026062741_AddSagaStatisticsProjection', N'10.0.0');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261027090000_AddTaskResponseTimeoutScheduling'
)
BEGIN
    ALTER TABLE [Runtime].[TaskExecutions] ADD [ResponseDeadlineUtc] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261027090000_AddTaskResponseTimeoutScheduling'
)
BEGIN
    ALTER TABLE [Runtime].[TaskExecutions] ADD [TimeoutActionId] nvarchar(64) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261027090000_AddTaskResponseTimeoutScheduling'
)
BEGIN
    ALTER TABLE [Runtime].[TaskExecutions] ADD [TimeoutActionLane] nvarchar(128) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261027090000_AddTaskResponseTimeoutScheduling'
)
BEGIN
    CREATE INDEX [IX_TaskExecutions_Status_ResponseDeadlineUtc] ON [Runtime].[TaskExecutions] ([Status], [ResponseDeadlineUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261027090000_AddTaskResponseTimeoutScheduling'
)
BEGIN
    CREATE INDEX [IX_TaskExecutions_Status_TimeoutActionId] ON [Runtime].[TaskExecutions] ([Status], [TimeoutActionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261027090000_AddTaskResponseTimeoutScheduling'
)
BEGIN
    INSERT INTO [Runtime].[__RuntimeStorageMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261027090000_AddTaskResponseTimeoutScheduling', N'10.0.0');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261028100000_AddIdleReconciliationWindow'
)
BEGIN
    ALTER TABLE [Runtime].[OrchestrationInstances] ADD [IdleExpiresOnUtc] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261028100000_AddIdleReconciliationWindow'
)
BEGIN
    ALTER TABLE [Runtime].[OrchestrationInstances] ADD [IdleSinceUtc] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261028100000_AddIdleReconciliationWindow'
)
BEGIN
    UPDATE [Runtime].[OrchestrationInstances] SET [Status] = 'Running', [WaitingSinceUtc] = NULL WHERE [Status] = 'Waiting';
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261028100000_AddIdleReconciliationWindow'
)
BEGIN
    CREATE INDEX [IX_OrchestrationInstances_Status_IdleExpiresOnUtc] ON [Runtime].[OrchestrationInstances] ([Status], [IdleExpiresOnUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261028100000_AddIdleReconciliationWindow'
)
BEGIN
    INSERT INTO [Runtime].[__RuntimeStorageMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261028100000_AddIdleReconciliationWindow', N'10.0.0');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261030043310_AddLastExecutionCursor'
)
BEGIN
    ALTER TABLE [Runtime].[OrchestrationInstances] ADD [LastExecutedStageKey] nvarchar(256) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261030043310_AddLastExecutionCursor'
)
BEGIN
    ALTER TABLE [Runtime].[OrchestrationInstances] ADD [LastExecutedTaskKey] nvarchar(256) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261030043310_AddLastExecutionCursor'
)
BEGIN
    INSERT INTO [Runtime].[__RuntimeStorageMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261030043310_AddLastExecutionCursor', N'10.0.0');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261030051000_BackfillLastExecutionCursor'
)
BEGIN
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
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261030051000_BackfillLastExecutionCursor'
)
BEGIN
    INSERT INTO [Runtime].[__RuntimeStorageMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261030051000_BackfillLastExecutionCursor', N'10.0.0');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261101010000_AddInstanceProgressProjection'
)
BEGIN
    ALTER TABLE [Runtime].[OrchestrationInstances] ADD [OrchestrationVersion] nvarchar(64) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261101010000_AddInstanceProgressProjection'
)
BEGIN
    ALTER TABLE [Runtime].[OrchestrationInstances] ADD [TotalTaskCount] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261101010000_AddInstanceProgressProjection'
)
BEGIN
    ALTER TABLE [Runtime].[OrchestrationInstances] ADD [CompletedTaskCount] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261101010000_AddInstanceProgressProjection'
)
BEGIN
    INSERT INTO [Runtime].[__RuntimeStorageMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261101010000_AddInstanceProgressProjection', N'10.0.0');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261102060000_FilterStatisticsBackfillIndex'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_OrchestrationInstances_StatisticsRevisionReplacement] ON [Runtime].[OrchestrationInstances] ([StatisticsRevision]) WHERE [StatisticsRevision] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261102060000_FilterStatisticsBackfillIndex'
)
BEGIN
    DROP INDEX [IX_OrchestrationInstances_StatisticsRevision] ON [Runtime].[OrchestrationInstances];
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261102060000_FilterStatisticsBackfillIndex'
)
BEGIN
    EXEC sp_rename N'[Runtime].[OrchestrationInstances].[IX_OrchestrationInstances_StatisticsRevisionReplacement]', N'IX_OrchestrationInstances_StatisticsRevision', N'INDEX';
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261102060000_FilterStatisticsBackfillIndex'
)
BEGIN
    INSERT INTO [Runtime].[__RuntimeStorageMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261102060000_FilterStatisticsBackfillIndex', N'10.0.0');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261102070000_FilterStatisticsActivityIndex'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_SagaStatisticsObservations_ActivityReplacement] ON [Runtime].[SagaStatisticsObservations] ([OrchestrationKey], [OccurredOnUtc]) INCLUDE ([Status]) WHERE [Processed] = 1 AND [StatusChanged] = 1 AND [IsBackfill] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261102070000_FilterStatisticsActivityIndex'
)
BEGIN
    DROP INDEX [IX_SagaStatisticsObservations_OrchestrationKey_OccurredOnUtc] ON [Runtime].[SagaStatisticsObservations];
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261102070000_FilterStatisticsActivityIndex'
)
BEGIN
    EXEC sp_rename N'[Runtime].[SagaStatisticsObservations].[IX_SagaStatisticsObservations_ActivityReplacement]', N'IX_SagaStatisticsObservations_OrchestrationKey_OccurredOnUtc', N'INDEX';
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261102070000_FilterStatisticsActivityIndex'
)
BEGIN
    INSERT INTO [Runtime].[__RuntimeStorageMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261102070000_FilterStatisticsActivityIndex', N'10.0.0');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261106010000_AddInstanceExpirationProjection'
)
BEGIN
    ALTER TABLE [Runtime].[OrchestrationInstances] ADD [MaxInstanceDurationSeconds] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261106010000_AddInstanceExpirationProjection'
)
BEGIN
    ALTER TABLE [Runtime].[OrchestrationInstances] ADD [IdleDurationSeconds] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261106010000_AddInstanceExpirationProjection'
)
BEGIN
    ALTER TABLE [Runtime].[OrchestrationInstances] ADD [ExpiresOnUtc] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261106010000_AddInstanceExpirationProjection'
)
BEGIN
    CREATE INDEX [IX_OrchestrationInstances_Status_ExpiresOnUtc_Id] ON [Runtime].[OrchestrationInstances] ([Status], [ExpiresOnUtc], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261106010000_AddInstanceExpirationProjection'
)
BEGIN
    INSERT INTO [Runtime].[__RuntimeStorageMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261106010000_AddInstanceExpirationProjection', N'10.0.0');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261111005231_AddSagaFailureProjection'
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
    WHERE [MigrationId] = N'20261111005231_AddSagaFailureProjection'
)
BEGIN
    CREATE TABLE [Runtime].[SagaFailureEvents] (
        [Id] binary(16) NOT NULL,
        [OrchestrationInstanceId] binary(16) NOT NULL,
        [OccurredOnUtc] datetime2 NOT NULL,
        [TransitionType] nvarchar(128) NOT NULL,
        CONSTRAINT [PK_SagaFailureEvents] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261111005231_AddSagaFailureProjection'
)
BEGIN
    CREATE INDEX [IX_SagaFailureEvents_TransitionType_OccurredOnUtc] ON [Runtime].[SagaFailureEvents] ([TransitionType], [OccurredOnUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261111005231_AddSagaFailureProjection'
)
BEGIN
    INSERT INTO [Runtime].[__RuntimeStorageMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261111005231_AddSagaFailureProjection', N'10.0.0');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261111210042_AddFailureEventScope'
)
BEGIN
    ALTER TABLE [Runtime].[SagaFailureEvents] ADD [OrchestrationKey] nvarchar(256) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261111210042_AddFailureEventScope'
)
BEGIN
    ALTER TABLE [Runtime].[SagaFailureEvents] ADD [OrchestrationVersion] nvarchar(64) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261111210042_AddFailureEventScope'
)
BEGIN
    ALTER TABLE [Runtime].[SagaFailureEvents] ADD [ScopeProjectionVersion] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261111210042_AddFailureEventScope'
)
BEGIN
    CREATE INDEX [IX_SagaFailureEvents_OrchestrationKey_OrchestrationVersion_TransitionType_OccurredOnUtc] ON [Runtime].[SagaFailureEvents] ([OrchestrationKey], [OrchestrationVersion], [TransitionType], [OccurredOnUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261111210042_AddFailureEventScope'
)
BEGIN
    CREATE INDEX [IX_SagaFailureEvents_ScopeProjectionVersion_OccurredOnUtc] ON [Runtime].[SagaFailureEvents] ([ScopeProjectionVersion], [OccurredOnUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261111210042_AddFailureEventScope'
)
BEGIN
    INSERT INTO [Runtime].[__RuntimeStorageMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261111210042_AddFailureEventScope', N'10.0.0');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261119152406_RemoveCrossSagaContention'
)
BEGIN
    ALTER TABLE [Runtime].[TaskDispatches] ADD [CallbackAcceptedOnUtc] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261119152406_RemoveCrossSagaContention'
)
BEGIN
    ALTER TABLE [Runtime].[TaskDispatches] ADD [CallbackPending] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261119152406_RemoveCrossSagaContention'
)
BEGIN
    ALTER TABLE [Runtime].[TaskDispatches] ADD [CallbackReceiptId] nvarchar(64) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261119152406_RemoveCrossSagaContention'
)
BEGIN
    ALTER TABLE [Runtime].[TaskDispatches] ADD [PendingCallbackPayload] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261119152406_RemoveCrossSagaContention'
)
BEGIN
    ALTER TABLE [Runtime].[RuntimeIngressConfigurations] ADD [RouteTopic] nvarchar(512) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261119152406_RemoveCrossSagaContention'
)
BEGIN
    ALTER TABLE [Runtime].[RuntimeIngressConfigurations] ADD [RouteVersion] nvarchar(64) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261119152406_RemoveCrossSagaContention'
)
BEGIN
    ALTER TABLE [Runtime].[DurableAction] ADD [DeferralReasonCode] nvarchar(128) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261119152406_RemoveCrossSagaContention'
)
BEGIN
    ALTER TABLE [Runtime].[DurableAction] ADD [PendingReason] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261119152406_RemoveCrossSagaContention'
)
BEGIN
    EXEC(N'
        UPDATE [Runtime].[RuntimeIngressConfigurations]
        SET [RouteTopic] = UPPER(COALESCE(JSON_VALUE([SettingsPayload], ''$.topic''), JSON_VALUE([SettingsPayload], ''$.Topic''))),
            [RouteVersion] = UPPER(COALESCE(JSON_VALUE([SettingsPayload], ''$.version''), JSON_VALUE([SettingsPayload], ''$.Version'')))
        WHERE [IngressTransport] = ''Messaging'' AND ISJSON([SettingsPayload]) = 1;
    ')
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261119152406_RemoveCrossSagaContention'
)
BEGIN
    CREATE INDEX [IX_TaskDispatches_CallbackPending_CallbackAcceptedOnUtc] ON [Runtime].[TaskDispatches] ([CallbackPending], [CallbackAcceptedOnUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261119152406_RemoveCrossSagaContention'
)
BEGIN
    CREATE INDEX [IX_RuntimeIngressConfigurations_RouteTopic_RouteVersion_IsActive] ON [Runtime].[RuntimeIngressConfigurations] ([RouteTopic], [RouteVersion], [IsActive]);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261119152406_RemoveCrossSagaContention'
)
BEGIN
    CREATE INDEX [IX_DurableAction_Status_PendingReason_NextAttemptOnUtc] ON [Runtime].[DurableAction] ([Status], [PendingReason], [NextAttemptOnUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261119152406_RemoveCrossSagaContention'
)
BEGIN
    INSERT INTO [Runtime].[__RuntimeStorageMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261119152406_RemoveCrossSagaContention', N'10.0.0');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261123082716_AlignRuntimeConcurrencyModel'
)
BEGIN
    DROP INDEX [IX_TaskExecutions_Status] ON [Runtime].[TaskExecutions];
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261123082716_AlignRuntimeConcurrencyModel'
)
BEGIN
    DROP INDEX [IX_TaskExecutions_Status_ResponseDeadlineUtc] ON [Runtime].[TaskExecutions];
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261123082716_AlignRuntimeConcurrencyModel'
)
BEGIN
    DROP INDEX [IX_TaskExecutions_Status_TimeoutActionId] ON [Runtime].[TaskExecutions];
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261123082716_AlignRuntimeConcurrencyModel'
)
BEGIN
    ALTER TABLE [Runtime].[TaskExecutionAttempts] DROP CONSTRAINT [PK_TaskExecutionAttempts];
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261123082716_AlignRuntimeConcurrencyModel'
)
BEGIN
    DROP INDEX [IX_TaskExecutionAttempts_Status] ON [Runtime].[TaskExecutionAttempts];
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261123082716_AlignRuntimeConcurrencyModel'
)
BEGIN
    ALTER TABLE [Runtime].[TaskDispatches] DROP CONSTRAINT [PK_TaskDispatches];
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261123082716_AlignRuntimeConcurrencyModel'
)
BEGIN
    DROP INDEX [IX_TaskDispatches_CallbackPending_CallbackAcceptedOnUtc] ON [Runtime].[TaskDispatches];
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261123082716_AlignRuntimeConcurrencyModel'
)
BEGIN
    DROP INDEX [IX_TaskDispatches_DispatchStatus] ON [Runtime].[TaskDispatches];
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261123082716_AlignRuntimeConcurrencyModel'
)
BEGIN
    DROP INDEX [IX_TaskDispatches_PhysicalCancellationPending] ON [Runtime].[TaskDispatches];
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261123082716_AlignRuntimeConcurrencyModel'
)
BEGIN
    DROP INDEX [IX_SagaStatisticsObservations_InstanceId_Revision] ON [Runtime].[SagaStatisticsObservations];
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261123082716_AlignRuntimeConcurrencyModel'
)
BEGIN
    DROP INDEX [IX_SagaStatisticsObservations_Processed_InstanceId_Revision] ON [Runtime].[SagaStatisticsObservations];
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261123082716_AlignRuntimeConcurrencyModel'
)
BEGIN
    DROP INDEX [IX_OrchestrationInstances_DispatchCancellationPending_LastUpdatedOnUtc] ON [Runtime].[OrchestrationInstances];
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261123082716_AlignRuntimeConcurrencyModel'
)
BEGIN
    DROP INDEX [IX_OrchestrationInstances_LastUpdatedOnUtc] ON [Runtime].[OrchestrationInstances];
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261123082716_AlignRuntimeConcurrencyModel'
)
BEGIN
    DROP INDEX [IX_OrchestrationInstances_SagaId] ON [Runtime].[OrchestrationInstances];
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261123082716_AlignRuntimeConcurrencyModel'
)
BEGIN
    DROP INDEX [IX_OrchestrationInstances_StartedOnUtc_Status] ON [Runtime].[OrchestrationInstances];
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261123082716_AlignRuntimeConcurrencyModel'
)
BEGIN
    DROP INDEX [IX_OrchestrationInstances_Status_ExpiresOnUtc_Id] ON [Runtime].[OrchestrationInstances];
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261123082716_AlignRuntimeConcurrencyModel'
)
BEGIN
    DROP INDEX [IX_OrchestrationInstances_Status_IdleExpiresOnUtc] ON [Runtime].[OrchestrationInstances];
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261123082716_AlignRuntimeConcurrencyModel'
)
BEGIN
    DROP INDEX [IX_OrchestrationInstances_Status_LastUpdatedOnUtc] ON [Runtime].[OrchestrationInstances];
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261123082716_AlignRuntimeConcurrencyModel'
)
BEGIN
    CREATE SEQUENCE [Runtime].[RuntimeStorageSequence] START WITH 1 INCREMENT BY 1 NO CYCLE;
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261123082716_AlignRuntimeConcurrencyModel'
)
BEGIN
    ALTER TABLE [Runtime].[TaskExecutionAttempts] ADD [StorageSequence] bigint NOT NULL DEFAULT (NEXT VALUE FOR [Runtime].[RuntimeStorageSequence]);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261123082716_AlignRuntimeConcurrencyModel'
)
BEGIN
    ALTER TABLE [Runtime].[TaskDispatches] ADD [StorageSequence] bigint NOT NULL DEFAULT (NEXT VALUE FOR [Runtime].[RuntimeStorageSequence]);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261123082716_AlignRuntimeConcurrencyModel'
)
BEGIN
    ALTER TABLE [Runtime].[TaskExecutionAttempts] ADD CONSTRAINT [PK_TaskExecutionAttempts] PRIMARY KEY NONCLUSTERED ([Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261123082716_AlignRuntimeConcurrencyModel'
)
BEGIN
    ALTER TABLE [Runtime].[TaskDispatches] ADD CONSTRAINT [PK_TaskDispatches] PRIMARY KEY NONCLUSTERED ([Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261123082716_AlignRuntimeConcurrencyModel'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_TaskExecutions_LegacyWaiting_WaitingSinceUtc_Id] ON [Runtime].[TaskExecutions] ([WaitingSinceUtc], [Id]) WHERE [Status] = ''WaitingResponse'' AND [ResponseDeadlineUtc] IS NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261123082716_AlignRuntimeConcurrencyModel'
)
BEGIN
    CREATE INDEX [IX_TaskExecutions_OrchestrationInstanceId] ON [Runtime].[TaskExecutions] ([OrchestrationInstanceId]);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261123082716_AlignRuntimeConcurrencyModel'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_TaskExecutions_Waiting_ResponseDeadlineUtc_Id] ON [Runtime].[TaskExecutions] ([ResponseDeadlineUtc], [Id]) WHERE [Status] = ''WaitingResponse'' AND [ResponseDeadlineUtc] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261123082716_AlignRuntimeConcurrencyModel'
)
BEGIN
    CREATE UNIQUE CLUSTERED INDEX [CIX_TaskExecutionAttempts_StorageSequence] ON [Runtime].[TaskExecutionAttempts] ([StorageSequence]);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261123082716_AlignRuntimeConcurrencyModel'
)
BEGIN
    CREATE UNIQUE CLUSTERED INDEX [CIX_TaskDispatches_StorageSequence] ON [Runtime].[TaskDispatches] ([StorageSequence]);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261123082716_AlignRuntimeConcurrencyModel'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_TaskDispatches_CallbackPending_CallbackAcceptedOnUtc] ON [Runtime].[TaskDispatches] ([CallbackPending], [CallbackAcceptedOnUtc]) WHERE [CallbackPending] = 1');
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261123082716_AlignRuntimeConcurrencyModel'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_TaskDispatches_PhysicalCancellationPending] ON [Runtime].[TaskDispatches] ([PhysicalCancellationPending]) WHERE [PhysicalCancellationPending] = 1');
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261123082716_AlignRuntimeConcurrencyModel'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_TaskDispatches_QueuePending_Id_ScheduledOnUtc] ON [Runtime].[TaskDispatches] ([ScheduledOnUtc], [Id]) WHERE [DispatchStatus] = ''QueuePending''');
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261123082716_AlignRuntimeConcurrencyModel'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_SagaStatisticsObservations_Pending_InstanceId_Revision] ON [Runtime].[SagaStatisticsObservations] ([InstanceId], [Revision]) WHERE [Processed] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261123082716_AlignRuntimeConcurrencyModel'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_OrchestrationInstances_DispatchCancellationPending_Id] ON [Runtime].[OrchestrationInstances] ([Id]) WHERE [DispatchCancellationPending] = 1');
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261123082716_AlignRuntimeConcurrencyModel'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_OrchestrationInstances_Idle_IdleExpiresOnUtc_Id] ON [Runtime].[OrchestrationInstances] ([IdleExpiresOnUtc], [Id]) WHERE [Status] = ''Idle'' AND [IdleExpiresOnUtc] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261123082716_AlignRuntimeConcurrencyModel'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_OrchestrationInstances_Running_ExpiresOnUtc_Id] ON [Runtime].[OrchestrationInstances] ([ExpiresOnUtc], [Id]) WHERE [Status] = ''Running'' AND [ExpiresOnUtc] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261123082716_AlignRuntimeConcurrencyModel'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_OrchestrationInstances_Running_StartedOnUtc_Id] ON [Runtime].[OrchestrationInstances] ([StartedOnUtc], [Id]) WHERE [Status] = ''Running''');
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261123082716_AlignRuntimeConcurrencyModel'
)
BEGIN
    CREATE INDEX [IX_DurableAction_CorrelationId] ON [Runtime].[DurableAction] ([CorrelationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [Runtime].[__RuntimeStorageMigrationsHistory]
    WHERE [MigrationId] = N'20261123082716_AlignRuntimeConcurrencyModel'
)
BEGIN
    INSERT INTO [Runtime].[__RuntimeStorageMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261123082716_AlignRuntimeConcurrencyModel', N'10.0.0');
END;

COMMIT;
GO

