using Krackend.Sagas.Orchestrations.Runtime.Operations;
using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Operations;

/// <summary>
/// Checks the runtime primary persistence dependency through the configured Entity Framework provider.
/// </summary>
internal sealed class EntityFrameworkRuntimePrimaryPersistenceProbe : IRuntimeDependencyProbe
{
    private readonly RuntimeDbContext _dbContext;
    private readonly RuntimeSchemaProbeCache _schemaCache;

    public EntityFrameworkRuntimePrimaryPersistenceProbe(RuntimeDbContext dbContext, RuntimeSchemaProbeCache schemaCache)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _schemaCache = schemaCache ?? throw new ArgumentNullException(nameof(schemaCache));
    }

    public string Name => "primary-persistence";

    public RuntimeDependencyKind Kind => RuntimeDependencyKind.Critical;

    public async ValueTask<RuntimeDependencyProbeResult> CheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var canConnect = await _dbContext.Database.CanConnectAsync(cancellationToken);
            if (canConnect)
            {
                var schemaError = await _schemaCache.CheckAsync(async token =>
                {
                    // Validate the projection required by failure and timeout transitions even
                    // when the table is empty; do not load artifacts or instance rows.
                    await _dbContext.SagaFailureEvents.AsNoTracking()
                        .Select(item => new { item.OrchestrationKey, item.OrchestrationVersion, item.ScopeProjectionVersion })
                        .Take(1).ToArrayAsync(token);
                }, cancellationToken);
                if (schemaError is not null)
                    return new RuntimeDependencyProbeResult(Name, Kind, false, $"Runtime failure-event schema is unavailable: {schemaError}");
            }
            return new RuntimeDependencyProbeResult(
                Name,
                Kind,
                canConnect,
                canConnect ? null : "Runtime database is not reachable.");
        }
        catch (Exception exception)
        {
            return new RuntimeDependencyProbeResult(Name, Kind, false, exception.Message);
        }
    }
}

/// <summary>Bounds schema checks to one per minute per runtime replica.</summary>
internal sealed class RuntimeSchemaProbeCache
{
    public RuntimeSchemaProbeCache() { }
    private readonly SemaphoreSlim _gate = new(1, 1);
    private DateTime _nextCheckUtc;
    private string _error;

    public async Task<string> CheckAsync(Func<CancellationToken, Task> check, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (DateTime.UtcNow < _nextCheckUtc) return _error;
            try
            {
                await check(cancellationToken);
                _error = null;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception exception) { _error = exception.Message; }
            _nextCheckUtc = DateTime.UtcNow.AddMinutes(1);
            return _error;
        }
        finally { _gate.Release(); }
    }
}
