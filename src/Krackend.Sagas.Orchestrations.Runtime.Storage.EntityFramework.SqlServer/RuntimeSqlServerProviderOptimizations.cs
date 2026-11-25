using System.Data;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.SqlServer;

internal sealed class RuntimeSqlServerProviderOptimizations
{
    private const string ProviderName = "Microsoft.EntityFrameworkCore.SqlServer";
    private readonly RuntimeSqlServerOptimizationOptions _options;
    private readonly RuntimeSqlServerDurableLogFlushCoordinator _flushCoordinator = new();

    public RuntimeSqlServerProviderOptimizations(RuntimeSqlServerOptimizationOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<bool?> TryAcquireTransactionLockAsync(
        RuntimeDbContext dbContext,
        Id instanceId,
        string leaseId,
        CancellationToken cancellationToken)
    {
        if (!_options.UseTransactionApplicationLocks ||
            !string.Equals(dbContext.Database.ProviderName, ProviderName, StringComparison.Ordinal) ||
            dbContext.Database.CurrentTransaction is null)
            return null;

        var connection = dbContext.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.Transaction = dbContext.Database.CurrentTransaction.GetDbTransaction();
        command.CommandType = CommandType.Text;
        command.CommandText = """
            DECLARE @result int;
            EXEC @result = sys.sp_getapplock
                @Resource = @resource,
                @LockMode = N'Exclusive',
                @LockOwner = N'Transaction',
                @LockTimeout = 0,
                @DbPrincipal = N'public';
            SELECT @result;
            """;
        var resource = command.CreateParameter();
        resource.ParameterName = "@resource";
        resource.DbType = DbType.String;
        resource.Size = 255;
        resource.Value = $"Krackend:OrchestrationInstance:{instanceId}";
        command.Parameters.Add(resource);

        var scalar = await command.ExecuteScalarAsync(cancellationToken);
        var result = Convert.ToInt32(scalar, System.Globalization.CultureInfo.InvariantCulture);
        if (result >= 0) return true;
        if (result is -1 or -2 or -3) return false;
        throw new InvalidOperationException(
            $"SQL Server could not acquire the orchestration transaction lock for '{instanceId}' (result {result}).");
    }

    public Task<TaskDispatchCallbackExecutionState?> TryReadCallbackGraphAsync(
        RuntimeDbContext dbContext,
        Id dispatchId,
        CancellationToken cancellationToken)
    {
        if (!_options.UseCompactCallbackGraphReader ||
            !string.Equals(dbContext.Database.ProviderName, ProviderName, StringComparison.Ordinal))
            return Task.FromResult<TaskDispatchCallbackExecutionState?>(null);
        return SqlServerCallbackGraphReader.TryReadAsync(dbContext, dispatchId, cancellationToken);
    }

    public Task AwaitDurabilityAsync(RuntimeDbContext dbContext)
    {
        if (!_options.CoordinateDurableLogFlushes ||
            !string.Equals(dbContext.Database.ProviderName, ProviderName, StringComparison.Ordinal))
            return Task.CompletedTask;
        return _flushCoordinator.FlushAsync(
            dbContext,
            _options.DurableLogFlushWindow,
            _options.DurableLogFlushConcurrency);
    }
}
