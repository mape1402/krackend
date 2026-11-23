BEGIN TRANSACTION;
IF EXISTS (SELECT 1 FROM [Runtime].[TaskDispatches] WHERE [CallbackPending] = 1)
    THROW 51000, 'Process accepted callback receipts before removing their durable payload columns.', 1;

DROP INDEX [IX_TaskDispatches_CallbackPending_CallbackAcceptedOnUtc] ON [Runtime].[TaskDispatches];

DROP INDEX [IX_RuntimeIngressConfigurations_RouteTopic_RouteVersion_IsActive] ON [Runtime].[RuntimeIngressConfigurations];

DROP INDEX [IX_DurableAction_Status_PendingReason_NextAttemptOnUtc] ON [Runtime].[DurableAction];

DECLARE @var nvarchar(max);
SELECT @var = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Runtime].[TaskDispatches]') AND [c].[name] = N'CallbackAcceptedOnUtc');
IF @var IS NOT NULL EXEC(N'ALTER TABLE [Runtime].[TaskDispatches] DROP CONSTRAINT ' + @var + ';');
ALTER TABLE [Runtime].[TaskDispatches] DROP COLUMN [CallbackAcceptedOnUtc];

DECLARE @var1 nvarchar(max);
SELECT @var1 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Runtime].[TaskDispatches]') AND [c].[name] = N'CallbackPending');
IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [Runtime].[TaskDispatches] DROP CONSTRAINT ' + @var1 + ';');
ALTER TABLE [Runtime].[TaskDispatches] DROP COLUMN [CallbackPending];

DECLARE @var2 nvarchar(max);
SELECT @var2 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Runtime].[TaskDispatches]') AND [c].[name] = N'CallbackReceiptId');
IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [Runtime].[TaskDispatches] DROP CONSTRAINT ' + @var2 + ';');
ALTER TABLE [Runtime].[TaskDispatches] DROP COLUMN [CallbackReceiptId];

DECLARE @var3 nvarchar(max);
SELECT @var3 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Runtime].[TaskDispatches]') AND [c].[name] = N'PendingCallbackPayload');
IF @var3 IS NOT NULL EXEC(N'ALTER TABLE [Runtime].[TaskDispatches] DROP CONSTRAINT ' + @var3 + ';');
ALTER TABLE [Runtime].[TaskDispatches] DROP COLUMN [PendingCallbackPayload];

DECLARE @var4 nvarchar(max);
SELECT @var4 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Runtime].[RuntimeIngressConfigurations]') AND [c].[name] = N'RouteTopic');
IF @var4 IS NOT NULL EXEC(N'ALTER TABLE [Runtime].[RuntimeIngressConfigurations] DROP CONSTRAINT ' + @var4 + ';');
ALTER TABLE [Runtime].[RuntimeIngressConfigurations] DROP COLUMN [RouteTopic];

DECLARE @var5 nvarchar(max);
SELECT @var5 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Runtime].[RuntimeIngressConfigurations]') AND [c].[name] = N'RouteVersion');
IF @var5 IS NOT NULL EXEC(N'ALTER TABLE [Runtime].[RuntimeIngressConfigurations] DROP CONSTRAINT ' + @var5 + ';');
ALTER TABLE [Runtime].[RuntimeIngressConfigurations] DROP COLUMN [RouteVersion];

DECLARE @var6 nvarchar(max);
SELECT @var6 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Runtime].[DurableAction]') AND [c].[name] = N'DeferralReasonCode');
IF @var6 IS NOT NULL EXEC(N'ALTER TABLE [Runtime].[DurableAction] DROP CONSTRAINT ' + @var6 + ';');
ALTER TABLE [Runtime].[DurableAction] DROP COLUMN [DeferralReasonCode];

DECLARE @var7 nvarchar(max);
SELECT @var7 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Runtime].[DurableAction]') AND [c].[name] = N'PendingReason');
IF @var7 IS NOT NULL EXEC(N'ALTER TABLE [Runtime].[DurableAction] DROP CONSTRAINT ' + @var7 + ';');
ALTER TABLE [Runtime].[DurableAction] DROP COLUMN [PendingReason];

DELETE FROM [Runtime].[__RuntimeStorageMigrationsHistory]
WHERE [MigrationId] = N'20261119152406_RemoveCrossSagaContention';

COMMIT;
GO

