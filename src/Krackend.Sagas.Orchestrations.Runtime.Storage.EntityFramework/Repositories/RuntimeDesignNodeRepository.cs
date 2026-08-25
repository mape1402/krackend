using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Distribution;
using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Repositories;

/// <summary>
/// Stores runtime design nodes in Entity Framework storage.
/// </summary>
internal sealed class RuntimeDesignNodeRepository : RuntimeRepositoryBase, IRuntimeDesignNodeRepository
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RuntimeDesignNodeRepository"/> class.
    /// </summary>
    public RuntimeDesignNodeRepository(RuntimeDbContext dbContext, IRuntimeStorageUnitOfWork unitOfWork)
        : base(dbContext, unitOfWork)
    {
    }

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<RuntimeDesignNode>> GetAllAsync(CancellationToken cancellationToken = default)
        => await DbContext.RuntimeDesignNodes.AsNoTracking()
            .OrderBy(x => x.Name)
            .ThenBy(x => x.Key)
            .ToArrayAsync(cancellationToken);

    /// <inheritdoc />
    public IReadOnlyCollection<RuntimeDesignNode> GetEnabled()
        => DbContext.RuntimeDesignNodes.AsNoTracking()
            .Where(x => x.IsEnabled)
            .OrderBy(x => x.Name)
            .ThenBy(x => x.Key)
            .ToArray();

    /// <inheritdoc />
    public async Task<RuntimeDesignNode> GetByIdAsync(Id id, CancellationToken cancellationToken = default)
        => await DbContext.RuntimeDesignNodes.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    /// <inheritdoc />
    public async Task<RuntimeDesignNode> GetByKeyAsync(string key, CancellationToken cancellationToken = default)
        => await DbContext.RuntimeDesignNodes.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Key == key, cancellationToken);

    /// <inheritdoc />
    public async Task UpsertAsync(RuntimeDesignNode designNode, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(designNode);

        var current = await DbContext.RuntimeDesignNodes.FirstOrDefaultAsync(x => x.Id == designNode.Id, cancellationToken);
        if (current is null)
        {
            DbContext.RuntimeDesignNodes.Add(designNode);
        }
        else
        {
            current.Key = designNode.Key;
            current.Name = designNode.Name;
            current.EndpointBaseUri = designNode.EndpointBaseUri;
            current.RemoteRuntimeNodeId = designNode.RemoteRuntimeNodeId;
            current.ClientId = designNode.ClientId;
            current.SecretReference = designNode.SecretReference;
            current.ProtectedSecret = designNode.ProtectedSecret;
            current.Description = designNode.Description;
            current.IsEnabled = designNode.IsEnabled;
            current.CreatedOnUtc = designNode.CreatedOnUtc;
            current.UpdatedOnUtc = designNode.UpdatedOnUtc;
            current.LastConnectionCheckedOnUtc = designNode.LastConnectionCheckedOnUtc;
            current.LastConnectionSucceeded = designNode.LastConnectionSucceeded;
            current.LastConnectionMessage = designNode.LastConnectionMessage;
        }

        await SaveChanges(cancellationToken);
    }

    /// <inheritdoc />
    public async Task SetEnabledAsync(Id id, bool isEnabled, CancellationToken cancellationToken = default)
    {
        var current = await DbContext.RuntimeDesignNodes.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (current is null)
        {
            return;
        }

        current.IsEnabled = isEnabled;
        current.UpdatedOnUtc = DateTime.UtcNow;
        await SaveChanges(cancellationToken);
    }
}
