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
    UPDATE [Runtime].[RuntimeIngressConfigurations]
    SET [RouteTopic] = UPPER(COALESCE(JSON_VALUE([SettingsPayload], '$.topic'), JSON_VALUE([SettingsPayload], '$.Topic'))),
        [RouteVersion] = UPPER(COALESCE(JSON_VALUE([SettingsPayload], '$.version'), JSON_VALUE([SettingsPayload], '$.Version')))
    WHERE [IngressTransport] = 'Messaging' AND ISJSON([SettingsPayload]) = 1;
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

