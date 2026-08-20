using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Ingress;
using Krackend.Sagas.Orchestrations.Runtime.Storage.SqlServer.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Krackend.Sagas.Orchestrations.Runtime.Storage.SqlServer.Repositories;

internal sealed class RuntimeIngressConfigurationRepository : RuntimeRepositoryBase, IRuntimeIngressConfigurationRepository
{
    public RuntimeIngressConfigurationRepository(RuntimeStorageDbContext dbContext, IRuntimeStorageUnitOfWork unitOfWork)
        : base(dbContext, unitOfWork)
    {
    }

    public async Task UpsertForArtifactAsync(
        Id runtimeOrchestrationArtifactId,
        IReadOnlyCollection<RuntimeIngressConfiguration> configurations,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var currentConfigurations = await DbContext.RuntimeIngressConfigurations
            .Where(x => x.RuntimeOrchestrationArtifactId == runtimeOrchestrationArtifactId)
            .ToArrayAsync(cancellationToken);
        var configurationsByKey = configurations.ToDictionary(x => x.ConfigurationKey, StringComparer.OrdinalIgnoreCase);

        foreach (var current in currentConfigurations)
        {
            if (!configurationsByKey.TryGetValue(current.ConfigurationKey, out var next))
            {
                current.IsActive = false;
                current.UpdatedOnUtc = now;
                current.DeactivatedOnUtc = now;
                continue;
            }

            current.IngressKind = next.IngressKind;
            current.IngressTransport = next.IngressTransport;
            current.SettingsPayload = next.SettingsPayload;
            current.IsActive = true;
            current.UpdatedOnUtc = next.UpdatedOnUtc;
            current.DeactivatedOnUtc = null;
            configurationsByKey.Remove(current.ConfigurationKey);
        }

        foreach (var configuration in configurationsByKey.Values)
        {
            DbContext.RuntimeIngressConfigurations.Add(configuration);
        }

        await SaveChanges(cancellationToken);
    }

    public async Task DeactivateForArtifactsAsync(
        IReadOnlyCollection<Id> runtimeOrchestrationArtifactIds,
        CancellationToken cancellationToken = default)
    {
        if (runtimeOrchestrationArtifactIds is null || runtimeOrchestrationArtifactIds.Count == 0)
        {
            return;
        }

        var now = DateTime.UtcNow;
        var configurations = await DbContext.RuntimeIngressConfigurations
            .Where(x => runtimeOrchestrationArtifactIds.Contains(x.RuntimeOrchestrationArtifactId) && x.IsActive)
            .ToArrayAsync(cancellationToken);

        foreach (var configuration in configurations)
        {
            configuration.IsActive = false;
            configuration.UpdatedOnUtc = now;
            configuration.DeactivatedOnUtc = now;
        }

        await SaveChanges(cancellationToken);
    }

    public async Task<IReadOnlyCollection<RuntimeIngressConfiguration>> ReadActiveAsync(
        int skip,
        int take,
        CancellationToken cancellationToken = default)
        => await DbContext.RuntimeIngressConfigurations.AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.RuntimeOrchestrationArtifactId)
            .ThenBy(x => x.ConfigurationKey)
            .Skip(skip)
            .Take(take)
            .ToArrayAsync(cancellationToken);

    public async Task<IReadOnlyCollection<RuntimeIngressConfiguration>> GetActiveByArtifactIdAsync(
        Id runtimeOrchestrationArtifactId,
        CancellationToken cancellationToken = default)
        => await DbContext.RuntimeIngressConfigurations.AsNoTracking()
            .Where(x => x.RuntimeOrchestrationArtifactId == runtimeOrchestrationArtifactId && x.IsActive)
            .OrderBy(x => x.ConfigurationKey)
            .ToArrayAsync(cancellationToken);
}
