using Microsoft.Data.SqlClient;

namespace Krackend.Sagas.Orchestrations.RuntimeHost.Sample.Bootstrap;

internal sealed class SqlServerDatabaseMigrationLock : IDatabaseMigrationLock
{
    public async Task<IDatabaseMigrationLockLease> AcquireAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        var builder = new SqlConnectionStringBuilder(connectionString);
        var databaseName = builder.InitialCatalog;
        builder.InitialCatalog = "master";

        var connection = new SqlConnection(builder.ConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        var lockName = $"Krackend.RuntimeHost.Migrations.{databaseName}";
        await using var command = connection.CreateCommand();
        command.CommandText = """
            DECLARE @result int;
            EXEC @result = sp_getapplock
                @Resource = @resource,
                @LockMode = 'Exclusive',
                @LockOwner = 'Session',
                @LockTimeout = @timeout;
            SELECT @result;
            """;
        command.Parameters.AddWithValue("@resource", lockName);
        command.Parameters.AddWithValue("@timeout", 120_000);

        var result = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false));
        if (result < 0)
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw new InvalidOperationException($"Could not acquire the migration lock '{lockName}'. SQL Server result: {result}.");
        }

        return new SqlServerDatabaseMigrationLockLease(connection, lockName);
    }
}
