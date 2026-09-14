using Microsoft.Data.SqlClient;

namespace Krackend.Sagas.Orchestrations.RuntimeHost.Sample.Bootstrap;

internal sealed class SqlServerDatabaseMigrationLockLease(SqlConnection connection, string lockName) : IDatabaseMigrationLockLease
{
    public async ValueTask DisposeAsync()
    {
        try
        {
            if (connection.State == System.Data.ConnectionState.Open)
            {
                await using var command = connection.CreateCommand();
                command.CommandText = "EXEC sp_releaseapplock @Resource = @resource, @LockOwner = 'Session';";
                command.Parameters.AddWithValue("@resource", lockName);
                await command.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
        }
        finally
        {
            await connection.DisposeAsync().ConfigureAwait(false);
        }
    }
}
