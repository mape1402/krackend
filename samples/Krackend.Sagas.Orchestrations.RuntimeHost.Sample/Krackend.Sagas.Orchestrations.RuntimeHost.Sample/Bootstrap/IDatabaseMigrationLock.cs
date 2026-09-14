namespace Krackend.Sagas.Orchestrations.RuntimeHost.Sample.Bootstrap;

internal interface IDatabaseMigrationLock
{
    Task<IDatabaseMigrationLockLease> AcquireAsync(string connectionString, CancellationToken cancellationToken = default);
}
