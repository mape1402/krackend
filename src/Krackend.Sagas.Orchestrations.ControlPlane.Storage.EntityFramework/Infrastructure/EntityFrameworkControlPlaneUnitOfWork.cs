using Krackend.Sagas.Orchestrations.ControlPlane.Storage;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Infrastructure;

/// <summary>
/// Persists pending changes through the control-plane Entity Framework context.
/// </summary>
public sealed class EntityFrameworkControlPlaneUnitOfWork : IControlPlaneUnitOfWork
{
    private readonly ControlPlaneDbContext _dbContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="EntityFrameworkControlPlaneUnitOfWork"/> class.
    /// </summary>
    /// <param name="dbContext">Control-plane Entity Framework context.</param>
    public EntityFrameworkControlPlaneUnitOfWork(ControlPlaneDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    /// <inheritdoc />
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => _dbContext.SaveChangesAsync(cancellationToken);
}
