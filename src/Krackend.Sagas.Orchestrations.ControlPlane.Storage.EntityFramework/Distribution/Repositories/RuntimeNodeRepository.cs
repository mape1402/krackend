using Microsoft.EntityFrameworkCore;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;
using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Enums;
using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Storage;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Distribution.Entities;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Infrastructure;
using Sieve.Models;
using Sieve.Services;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Distribution.Repositories;

public sealed class RuntimeNodeRepository : IRuntimeNodeRepository
{
    private readonly ControlPlaneDbContext _dbContext;
    private readonly ISieveProcessor _sieveProcessor;

    public RuntimeNodeRepository(ControlPlaneDbContext dbContext, ISieveProcessor sieveProcessor)
    {
        _dbContext = dbContext;
        _sieveProcessor = sieveProcessor;
    }

    public async Task Create(RuntimeNode runtimeNode, CancellationToken cancellationToken = default)
    {
        _dbContext.RuntimeNodes.Add(Map(runtimeNode));
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task Update(RuntimeNode runtimeNode, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.RuntimeNodes.FirstAsync(x => x.Id == runtimeNode.Id, cancellationToken);
        entity.Name = runtimeNode.Name;
        entity.Code = runtimeNode.Code;
        entity.EnvironmentId = runtimeNode.EnvironmentId;
        entity.DistributionMode = runtimeNode.DistributionMode;
        entity.EndpointBaseUri = runtimeNode.EndpointBaseUri;
        entity.EndpointApiPath = runtimeNode.EndpointApiPath;
        entity.Status = runtimeNode.Status;
        entity.IsEnabled = runtimeNode.Status == RuntimeNodeStatus.Enabled;
        entity.IsDeleted = runtimeNode.IsDeleted;
        entity.DeletedAtUtc = runtimeNode.DeletedAtUtc;
        entity.Description = runtimeNode.Description;
        entity.AccessTokenTtlSeconds = runtimeNode.AccessTokenTtlSeconds;
        entity.TokenRefreshSkewSeconds = runtimeNode.TokenRefreshSkewSeconds;
        entity.TokenValidationCacheTtlSeconds = runtimeNode.TokenValidationCacheTtlSeconds;
        entity.InboundClientId = runtimeNode.InboundClientId;
        entity.InboundKeyId = runtimeNode.InboundKeyId;
        entity.InboundSecretHash = runtimeNode.InboundSecretHash;
        entity.InboundAllowedScopes = runtimeNode.InboundAllowedScopes;
        entity.InboundCredentialStatus = runtimeNode.InboundCredentialStatus;
        entity.InboundCredentialCreatedAtUtc = runtimeNode.InboundCredentialCreatedAtUtc;
        entity.InboundCredentialRotatedAtUtc = runtimeNode.InboundCredentialRotatedAtUtc;
        entity.InboundCredentialRevokedAtUtc = runtimeNode.InboundCredentialRevokedAtUtc;
        entity.InboundLastTokenIssuedAtUtc = runtimeNode.InboundLastTokenIssuedAtUtc;
        entity.InboundLastTokenFailedAtUtc = runtimeNode.InboundLastTokenFailedAtUtc;
        entity.InboundLastFailureReason = runtimeNode.InboundLastFailureReason;
        entity.OutboundClientId = runtimeNode.OutboundClientId;
        entity.OutboundKeyId = runtimeNode.OutboundKeyId;
        entity.ProtectedOutboundSecret = runtimeNode.ProtectedOutboundSecret;
        entity.OutboundRequestedScopes = runtimeNode.OutboundRequestedScopes;
        entity.OutboundCredentialStatus = runtimeNode.OutboundCredentialStatus;
        entity.OutboundCredentialImportedAtUtc = runtimeNode.OutboundCredentialImportedAtUtc;
        entity.OutboundLastTokenReceivedAtUtc = runtimeNode.OutboundLastTokenReceivedAtUtc;
        entity.LastUpdatedAtUtc = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SetStatus(Id runtimeNodeId, RuntimeNodeStatus status, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.RuntimeNodes.FirstAsync(x => x.Id == runtimeNodeId, cancellationToken);
        entity.Status = status;
        entity.IsEnabled = status == RuntimeNodeStatus.Enabled;
        entity.LastUpdatedAtUtc = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SoftDelete(Id runtimeNodeId, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.RuntimeNodes.FirstAsync(x => x.Id == runtimeNodeId, cancellationToken);
        entity.IsDeleted = true;
        entity.DeletedAtUtc = DateTime.UtcNow;
        entity.Status = RuntimeNodeStatus.Suspend;
        entity.IsEnabled = false;
        entity.LastUpdatedAtUtc = entity.DeletedAtUtc;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<RuntimeNode> GetById(Id runtimeNodeId, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.RuntimeNodes
            .AsNoTracking()
            .Include(x => x.Environment)
            .FirstAsync(x => x.Id == runtimeNodeId, cancellationToken);
        return Map(entity);
    }

    public async Task<RuntimeNode> GetByCode(string code, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.RuntimeNodes
            .AsNoTracking()
            .Include(x => x.Environment)
            .FirstOrDefaultAsync(x => !x.IsDeleted && x.Code == code, cancellationToken);
        return entity is null ? null : Map(entity);
    }

    public async Task<RuntimeNode> GetByInboundClientId(string clientId, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.RuntimeNodes
            .AsNoTracking()
            .Include(x => x.Environment)
            .FirstOrDefaultAsync(x => !x.IsDeleted && x.InboundClientId == clientId, cancellationToken);
        return entity is null ? null : Map(entity);
    }

    public async Task<PagedResult<RuntimeNode>> GetAll(PagedSettings pagedSettings, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.RuntimeNodes.AsNoTracking().Include(x => x.Environment).Where(x => !x.IsDeleted).OrderBy(x => x.Name);
        var processed = _sieveProcessor.Apply(new SieveModel { Page = pagedSettings.PageNumber, PageSize = pagedSettings.PageSize }, query);
        var rows = await processed.ToArrayAsync(cancellationToken);
        var totalRows = await query.CountAsync(cancellationToken);
        var totalPages = totalRows == 0 ? 1 : (int)Math.Ceiling(totalRows / (double)pagedSettings.PageSize);
        return new PagedResult<RuntimeNode>(pagedSettings.PageNumber, totalPages, totalRows, pagedSettings.PageSize, rows.Select(Map).ToArray());
    }

    private static RuntimeNodeEntity Map(RuntimeNode x) => new()
    {
        Id = x.Id,
        Name = x.Name,
        Code = x.Code,
        EnvironmentId = x.EnvironmentId,
        DistributionMode = x.DistributionMode,
        EndpointBaseUri = x.EndpointBaseUri,
        EndpointApiPath = x.EndpointApiPath,
        Status = x.Status,
        IsEnabled = x.Status == RuntimeNodeStatus.Enabled,
        IsDeleted = x.IsDeleted,
        DeletedAtUtc = x.DeletedAtUtc,
        Description = x.Description,
        AccessTokenTtlSeconds = x.AccessTokenTtlSeconds,
        TokenRefreshSkewSeconds = x.TokenRefreshSkewSeconds,
        TokenValidationCacheTtlSeconds = x.TokenValidationCacheTtlSeconds,
        InboundClientId = x.InboundClientId,
        InboundKeyId = x.InboundKeyId,
        InboundSecretHash = x.InboundSecretHash,
        InboundAllowedScopes = x.InboundAllowedScopes,
        InboundCredentialStatus = x.InboundCredentialStatus,
        InboundCredentialCreatedAtUtc = x.InboundCredentialCreatedAtUtc,
        InboundCredentialRotatedAtUtc = x.InboundCredentialRotatedAtUtc,
        InboundCredentialRevokedAtUtc = x.InboundCredentialRevokedAtUtc,
        InboundLastTokenIssuedAtUtc = x.InboundLastTokenIssuedAtUtc,
        InboundLastTokenFailedAtUtc = x.InboundLastTokenFailedAtUtc,
        InboundLastFailureReason = x.InboundLastFailureReason,
        OutboundClientId = x.OutboundClientId,
        OutboundKeyId = x.OutboundKeyId,
        ProtectedOutboundSecret = x.ProtectedOutboundSecret,
        OutboundRequestedScopes = x.OutboundRequestedScopes,
        OutboundCredentialStatus = x.OutboundCredentialStatus,
        OutboundCredentialImportedAtUtc = x.OutboundCredentialImportedAtUtc,
        OutboundLastTokenReceivedAtUtc = x.OutboundLastTokenReceivedAtUtc,
        RegisteredAtUtc = x.RegisteredAtUtc,
        LastUpdatedAtUtc = x.LastUpdatedAtUtc
    };

    private static RuntimeNode Map(RuntimeNodeEntity x) => new()
    {
        Id = x.Id,
        Name = x.Name,
        Code = x.Code,
        EnvironmentId = x.EnvironmentId,
        EnvironmentName = x.Environment?.Name ?? string.Empty,
        DistributionMode = x.DistributionMode,
        EndpointBaseUri = x.EndpointBaseUri,
        EndpointApiPath = x.EndpointApiPath,
        Status = x.Status,
        IsEnabled = x.Status == RuntimeNodeStatus.Enabled,
        IsDeleted = x.IsDeleted,
        DeletedAtUtc = x.DeletedAtUtc,
        Description = x.Description,
        AccessTokenTtlSeconds = x.AccessTokenTtlSeconds,
        TokenRefreshSkewSeconds = x.TokenRefreshSkewSeconds,
        TokenValidationCacheTtlSeconds = x.TokenValidationCacheTtlSeconds,
        InboundClientId = x.InboundClientId,
        InboundKeyId = x.InboundKeyId,
        InboundSecretHash = x.InboundSecretHash,
        InboundAllowedScopes = x.InboundAllowedScopes,
        InboundCredentialStatus = x.InboundCredentialStatus,
        InboundCredentialCreatedAtUtc = x.InboundCredentialCreatedAtUtc,
        InboundCredentialRotatedAtUtc = x.InboundCredentialRotatedAtUtc,
        InboundCredentialRevokedAtUtc = x.InboundCredentialRevokedAtUtc,
        InboundLastTokenIssuedAtUtc = x.InboundLastTokenIssuedAtUtc,
        InboundLastTokenFailedAtUtc = x.InboundLastTokenFailedAtUtc,
        InboundLastFailureReason = x.InboundLastFailureReason,
        OutboundClientId = x.OutboundClientId,
        OutboundKeyId = x.OutboundKeyId,
        ProtectedOutboundSecret = x.ProtectedOutboundSecret,
        OutboundRequestedScopes = x.OutboundRequestedScopes,
        OutboundCredentialStatus = x.OutboundCredentialStatus,
        OutboundCredentialImportedAtUtc = x.OutboundCredentialImportedAtUtc,
        OutboundLastTokenReceivedAtUtc = x.OutboundLastTokenReceivedAtUtc,
        RegisteredAtUtc = x.RegisteredAtUtc,
        LastUpdatedAtUtc = x.LastUpdatedAtUtc
    };
}
