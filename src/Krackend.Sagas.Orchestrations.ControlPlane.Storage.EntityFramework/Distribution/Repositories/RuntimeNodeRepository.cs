using Microsoft.EntityFrameworkCore;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;
using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Core;
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
        entity.AuthenticationMode = runtimeNode.AuthenticationMode;
        entity.ClientId = runtimeNode.ClientId;
        entity.SecretReference = runtimeNode.SecretReference;
        entity.ApiKeyReference = runtimeNode.ApiKeyReference;
        entity.Status = runtimeNode.Status;
        entity.IsEnabled = runtimeNode.IsEnabled;
        entity.Description = runtimeNode.Description;
        entity.LastUpdatedAtUtc = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SetIsEnabled(Id runtimeNodeId, bool isEnabled, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.RuntimeNodes.FirstAsync(x => x.Id == runtimeNodeId, cancellationToken);
        entity.IsEnabled = isEnabled;
        entity.LastUpdatedAtUtc = DateTime.UtcNow;
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

    public async Task<PagedResult<RuntimeNode>> GetAll(PagedSettings pagedSettings, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.RuntimeNodes.AsNoTracking().Include(x => x.Environment).OrderBy(x => x.Name);
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
        AuthenticationMode = x.AuthenticationMode,
        ClientId = x.ClientId,
        SecretReference = x.SecretReference,
        ApiKeyReference = x.ApiKeyReference,
        Status = x.Status,
        IsEnabled = x.IsEnabled,
        Description = x.Description,
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
        AuthenticationMode = x.AuthenticationMode,
        ClientId = x.ClientId,
        SecretReference = x.SecretReference,
        ApiKeyReference = x.ApiKeyReference,
        Status = x.Status,
        IsEnabled = x.IsEnabled,
        Description = x.Description,
        RegisteredAtUtc = x.RegisteredAtUtc,
        LastUpdatedAtUtc = x.LastUpdatedAtUtc
    };
}
