using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Runtime.Ingress;
using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Ingress;

internal sealed class RuntimeIngressConfigurationAccessor :
    IGetAllIngressConfigurationsAccessor,
    IGetIngressConfigurationByArtifactAccessor
{
    private const int PageSize = 500;
    private readonly RuntimeDbContext _dbContext;
    private int _skip;

    public RuntimeIngressConfigurationAccessor(RuntimeDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<IngressConfigurationReadingResult> ReadAsync(CancellationToken cancellationToken = default)
    {
        var rows = await _dbContext.RuntimeIngressConfigurations.AsNoTracking()
            .Join(
                _dbContext.RuntimeOrchestrationArtifacts.AsNoTracking(),
                configuration => configuration.RuntimeOrchestrationArtifactId,
                artifact => artifact.Id,
                (configuration, artifact) => new { Configuration = configuration, Artifact = artifact })
            .Where(x => x.Configuration.IsActive &&
                x.Artifact.IsActive &&
                x.Artifact.Status == RuntimeOrchestrationArtifactStatus.Ready)
            .OrderBy(x => x.Configuration.RuntimeOrchestrationArtifactId)
            .ThenBy(x => x.Configuration.ConfigurationKey)
            .Skip(_skip)
            .Take(PageSize)
            .ToArrayAsync(cancellationToken);
        var configurations = rows
            .Select(x => new IngressConfiguration
            {
                Id = x.Configuration.Id.ToString(),
                ArtifactId = x.Configuration.RuntimeOrchestrationArtifactId.ToString(),
                OrchestrationDefinitionKey = x.Artifact.OrchestrationDefinitionKey,
                OrchestrationVersion = x.Artifact.Version.ToString(),
                DeployedOnUtc = x.Artifact.DeployedOnUtc,
                IngressKind = x.Configuration.IngressKind,
                IngressTransport = x.Configuration.IngressTransport,
                SettingsPayload = x.Configuration.SettingsPayload
            })
            .ToArray();

        _skip += configurations.Length;

        return new IngressConfigurationReadingResult
        {
            HasMoreItems = configurations.Length == PageSize,
            Configurations = configurations
        };
    }

    public async Task<IReadOnlyCollection<IngressConfiguration>> GetConfigurationAsync(
        string artifactId,
        CancellationToken cancellationToken = default)
    {
        var runtimeArtifactId = new Id(Ulid.Parse(artifactId));
        var rows = await _dbContext.RuntimeIngressConfigurations.AsNoTracking()
            .Join(
                _dbContext.RuntimeOrchestrationArtifacts.AsNoTracking(),
                configuration => configuration.RuntimeOrchestrationArtifactId,
                artifact => artifact.Id,
                (configuration, artifact) => new { Configuration = configuration, Artifact = artifact })
            .Where(x => x.Configuration.RuntimeOrchestrationArtifactId == runtimeArtifactId &&
                x.Configuration.IsActive &&
                x.Artifact.IsActive &&
                x.Artifact.Status == RuntimeOrchestrationArtifactStatus.Ready)
            .OrderBy(x => x.Configuration.ConfigurationKey)
            .ToArrayAsync(cancellationToken);

        return rows
            .Select(x => new IngressConfiguration
            {
                Id = x.Configuration.Id.ToString(),
                ArtifactId = x.Configuration.RuntimeOrchestrationArtifactId.ToString(),
                OrchestrationDefinitionKey = x.Artifact.OrchestrationDefinitionKey,
                OrchestrationVersion = x.Artifact.Version.ToString(),
                DeployedOnUtc = x.Artifact.DeployedOnUtc,
                IngressKind = x.Configuration.IngressKind,
                IngressTransport = x.Configuration.IngressTransport,
                SettingsPayload = x.Configuration.SettingsPayload
            })
            .ToArray();
    }

    public void Dispose()
    {
    }
}
