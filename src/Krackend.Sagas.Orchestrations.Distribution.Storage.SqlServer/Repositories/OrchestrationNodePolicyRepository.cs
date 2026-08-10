using Microsoft.EntityFrameworkCore;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Distribution.Core;
using Krackend.Sagas.Orchestrations.Distribution.Storage;
using Krackend.Sagas.Orchestrations.Distribution.Storage.SqlServer.Entities;
using Krackend.Sagas.Orchestrations.Distribution.Storage.SqlServer.Infrastructure;

namespace Krackend.Sagas.Orchestrations.Distribution.Storage.SqlServer.Repositories;

public sealed class OrchestrationNodePolicyRepository : IOrchestrationNodePolicyRepository
{
    private readonly DistributionStorageDbContext _dbContext;

    public OrchestrationNodePolicyRepository(DistributionStorageDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task Replace(string orchestrationDefinitionId, IReadOnlyCollection<OrchestrationAllowedRuntimeNode> allowedNodes, CancellationToken cancellationToken = default)
    {
        var existing = await _dbContext.OrchestrationAllowedRuntimeNodes
            .Where(x => x.OrchestrationDefinitionId == orchestrationDefinitionId)
            .ToArrayAsync(cancellationToken);

        if (existing.Length > 0)
        {
            _dbContext.OrchestrationAllowedRuntimeNodes.RemoveRange(existing);
        }

        if (allowedNodes?.Count > 0)
        {
            _dbContext.OrchestrationAllowedRuntimeNodes.AddRange(allowedNodes.Select(x => new OrchestrationAllowedRuntimeNodeEntity
            {
                Id = x.Id,
                OrchestrationDefinitionId = x.OrchestrationDefinitionId,
                RuntimeNodeId = x.RuntimeNodeId,
                CreatedAtUtc = x.CreatedAtUtc,
                CreatedBy = x.CreatedBy
            }));
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<Id>> GetAllowedRuntimeNodeIds(string orchestrationDefinitionId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.OrchestrationAllowedRuntimeNodes
            .AsNoTracking()
            .Where(x => x.OrchestrationDefinitionId == orchestrationDefinitionId)
            .Select(x => x.RuntimeNodeId)
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<string, IReadOnlyCollection<Id>>> GetAllowedRuntimeNodeIdsByOrchestrationIds(
        IReadOnlyCollection<string> orchestrationDefinitionIds,
        CancellationToken cancellationToken = default)
    {
        var ids = orchestrationDefinitionIds?.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToArray() ?? Array.Empty<string>();
        if (ids.Length == 0)
        {
            return new Dictionary<string, IReadOnlyCollection<Id>>(StringComparer.Ordinal);
        }

        var rows = await _dbContext.OrchestrationAllowedRuntimeNodes
            .AsNoTracking()
            .Where(x => ids.Contains(x.OrchestrationDefinitionId))
            .Select(x => new { x.OrchestrationDefinitionId, x.RuntimeNodeId })
            .ToArrayAsync(cancellationToken);

        return rows
            .GroupBy(x => x.OrchestrationDefinitionId, StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyCollection<Id>)g.Select(x => x.RuntimeNodeId).Distinct().ToArray(),
                StringComparer.Ordinal);
    }
}

