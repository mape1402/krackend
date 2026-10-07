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

    public EntityFrameworkRuntimePrimaryPersistenceProbe(RuntimeDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public string Name => "primary-persistence";

    public RuntimeDependencyKind Kind => RuntimeDependencyKind.Critical;

    public async ValueTask<RuntimeDependencyProbeResult> CheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var canConnect = await _dbContext.Database.CanConnectAsync(cancellationToken);
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
