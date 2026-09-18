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
            .Where(x => x.Status == RuntimeDesignNodeStatus.Enabled && x.IsEnabled)
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
    public async Task<RuntimeDesignNode> GetByInboundClientIdAsync(string clientId, CancellationToken cancellationToken = default)
        => await DbContext.RuntimeDesignNodes.AsNoTracking()
            .FirstOrDefaultAsync(x => x.InboundClientId == clientId, cancellationToken);

    /// <inheritdoc />
    public async Task UpsertAsync(RuntimeDesignNode designNode, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(designNode);

        await EnsureInboundClientIdIsUniqueAsync(designNode, cancellationToken);
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
            current.DistributionMode = designNode.DistributionMode;
            current.AccessTokenTtlSeconds = designNode.AccessTokenTtlSeconds;
            current.TokenRefreshSkewSeconds = designNode.TokenRefreshSkewSeconds;
            current.TokenValidationCacheTtlSeconds = designNode.TokenValidationCacheTtlSeconds;
            current.InboundClientId = designNode.InboundClientId;
            current.InboundKeyId = designNode.InboundKeyId;
            current.InboundSecretHash = designNode.InboundSecretHash;
            current.InboundAllowedScopes = designNode.InboundAllowedScopes;
            current.InboundCredentialStatus = designNode.InboundCredentialStatus;
            current.InboundCredentialCreatedAtUtc = designNode.InboundCredentialCreatedAtUtc;
            current.InboundCredentialRotatedAtUtc = designNode.InboundCredentialRotatedAtUtc;
            current.InboundCredentialRevokedAtUtc = designNode.InboundCredentialRevokedAtUtc;
            current.InboundLastTokenIssuedAtUtc = designNode.InboundLastTokenIssuedAtUtc;
            current.InboundLastTokenFailedAtUtc = designNode.InboundLastTokenFailedAtUtc;
            current.InboundLastFailureReason = designNode.InboundLastFailureReason;
            current.OutboundClientId = designNode.OutboundClientId;
            current.OutboundKeyId = designNode.OutboundKeyId;
            current.ProtectedOutboundSecret = designNode.ProtectedOutboundSecret;
            current.OutboundRequestedScopes = designNode.OutboundRequestedScopes;
            current.OutboundCredentialStatus = designNode.OutboundCredentialStatus;
            current.OutboundCredentialImportedAtUtc = designNode.OutboundCredentialImportedAtUtc;
            current.OutboundLastTokenReceivedAtUtc = designNode.OutboundLastTokenReceivedAtUtc;
            current.Description = designNode.Description;
            current.Status = designNode.Status;
            current.IsEnabled = designNode.IsEnabled;
            current.CreatedOnUtc = designNode.CreatedOnUtc;
            current.UpdatedOnUtc = designNode.UpdatedOnUtc;
        }

        await SaveChanges(cancellationToken);
    }

    private async Task EnsureInboundClientIdIsUniqueAsync(RuntimeDesignNode designNode, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(designNode.InboundClientId))
        {
            return;
        }

        var duplicateInboundClient = await DbContext.RuntimeDesignNodes
            .AsNoTracking()
            .AnyAsync(
                x => x.Id != designNode.Id && x.InboundClientId == designNode.InboundClientId,
                cancellationToken);

        if (duplicateInboundClient)
        {
            throw new InvalidOperationException($"Runtime design node inbound client id '{designNode.InboundClientId}' is already registered.");
        }
    }

    /// <inheritdoc />
    public async Task SetEnabledAsync(Id id, bool isEnabled, CancellationToken cancellationToken = default)
        => await SetStatusAsync(
            id,
            isEnabled ? RuntimeDesignNodeStatus.Enabled : RuntimeDesignNodeStatus.Suspend,
            cancellationToken);

    /// <inheritdoc />
    public async Task SetStatusAsync(Id id, RuntimeDesignNodeStatus status, CancellationToken cancellationToken = default)
    {
        var current = await DbContext.RuntimeDesignNodes.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (current is null)
        {
            return;
        }

        current.Status = status;
        current.IsEnabled = status == RuntimeDesignNodeStatus.Enabled;
        current.UpdatedOnUtc = DateTime.UtcNow;
        await SaveChanges(cancellationToken);
    }
}
