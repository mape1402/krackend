using Microsoft.EntityFrameworkCore.Migrations;

namespace Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.SqlServer;

/// <summary>SQL Server migration operations recommended for the portable row-store runtime.</summary>
public static class RuntimeSqlServerMigrationBuilderExtensions
{
    /// <summary>
    /// Adds portable database and physical-index tuning to a host-owned EF migration.
    /// The generated script is intended to be executed once by a DBA; the runtime
    /// login needs no elevated permission afterward.
    /// </summary>
    public static MigrationBuilder ApplyKrackendSqlServerPortableTuning(this MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);
        migrationBuilder.Sql("""
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
            """, suppressTransaction: true);
        return migrationBuilder;
    }
}
