using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Runtime.Ingress;
using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Ingress;

internal sealed class RuntimeIngressConfigurationAccessor :
    IGetAllIngressConfigurationsAccessor,
    ISelectiveMessagingIngressAccessor,
    IGetIngressConfigurationByArtifactAccessor,
    IGetStagedIngressConfigurationAccessor,
    IGetRollbackIngressConfigurationAccessor,
    IGetArchivedBackchannelConfigurationAccessor
{
    private const int PageSize = 500;
    private readonly RuntimeDbContext _dbContext;
    private int _skip;

    public RuntimeIngressConfigurationAccessor(RuntimeDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<IReadOnlyCollection<IngressConfiguration>> ReadMatchingAsync(string topic, string version,
        CancellationToken cancellationToken = default)
    {
        var routeTopic = topic.ToUpperInvariant();
        var routeVersion = version.ToUpperInvariant();
        var rows = await _dbContext.RuntimeIngressConfigurations.AsNoTracking()
            .Where(x => x.IsActive && x.IngressTransport == IngressTransport.Messaging &&
                ((x.RouteTopic == routeTopic && x.RouteVersion == routeVersion) || x.RouteTopic == null || x.RouteVersion == null))
            .Join(_dbContext.RuntimeOrchestrationArtifacts.AsNoTracking().Where(x => x.IsActive && x.Status == RuntimeOrchestrationArtifactStatus.Ready),
                configuration => configuration.RuntimeOrchestrationArtifactId, artifact => artifact.Id,
                (configuration, artifact) => new { Configuration = configuration, Artifact = artifact })
            .ToArrayAsync(cancellationToken);
        return rows.Select(x => new IngressConfiguration
        {
            Id = x.Configuration.Id.ToString(), ArtifactId = x.Artifact.Id.ToString(),
            OrchestrationDefinitionKey = x.Artifact.OrchestrationDefinitionKey,
            OrchestrationVersion = x.Artifact.Version.ToString(), DeployedOnUtc = x.Artifact.DeployedOnUtc,
            IngressKind = x.Configuration.IngressKind, MessageDisposition = x.Configuration.MessageDisposition,
            IngressTransport = x.Configuration.IngressTransport, SettingsPayload = x.Configuration.SettingsPayload
        }).ToArray();
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
                MessageDisposition = x.Configuration.MessageDisposition,
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

    public Task<IReadOnlyCollection<IngressConfiguration>> GetConfigurationAsync(
        string artifactId,
        CancellationToken cancellationToken = default)
        => GetConfigurationCoreAsync(artifactId, null, cancellationToken);

    public Task<IReadOnlyCollection<IngressConfiguration>> GetStagedConfigurationAsync(
        string artifactId, long ingressGeneration, CancellationToken cancellationToken = default)
        => GetConfigurationCoreAsync(artifactId, ingressGeneration, cancellationToken);

    public async Task<IReadOnlyCollection<IngressConfiguration>> GetArchivedBackchannelsAsync(
        string artifactId, long ingressGeneration, CancellationToken cancellationToken = default)
    {
        var runtimeArtifactId = new Id(Ulid.Parse(artifactId));
        var rows = await _dbContext.RuntimeIngressConfigurations.AsNoTracking()
            .Join(_dbContext.RuntimeOrchestrationArtifacts.AsNoTracking(),
                configuration => configuration.RuntimeOrchestrationArtifactId, artifact => artifact.Id,
                (configuration, artifact) => new { Configuration = configuration, Artifact = artifact })
            .Where(x => x.Configuration.RuntimeOrchestrationArtifactId == runtimeArtifactId &&
                x.Configuration.IngressKind == IngressKind.Backchannel &&
                x.Configuration.MessageDisposition == IngressMessageDisposition.Process &&
                x.Artifact.LifecycleStatus == RuntimeOrchestrationLifecycleStatus.Archived &&
                ((!x.Artifact.IsActive && x.Artifact.Status == RuntimeOrchestrationArtifactStatus.Retired) ||
                 (x.Artifact.IsActive && x.Artifact.Status == RuntimeOrchestrationArtifactStatus.Ready && x.Configuration.IsActive)) &&
                x.Artifact.IngressGeneration == ingressGeneration)
            .OrderBy(x => x.Configuration.ConfigurationKey).ToArrayAsync(cancellationToken);
        return rows.Select(x => new IngressConfiguration
        {
            Id = x.Configuration.Id.ToString(), ArtifactId = x.Artifact.Id.ToString(), IsStaged = true,
            OrchestrationDefinitionKey = x.Artifact.OrchestrationDefinitionKey,
            OrchestrationVersion = x.Artifact.Version.ToString(), DeployedOnUtc = x.Artifact.DeployedOnUtc,
            IngressKind = x.Configuration.IngressKind, MessageDisposition = x.Configuration.MessageDisposition,
            IngressTransport = x.Configuration.IngressTransport, SettingsPayload = x.Configuration.SettingsPayload
        }).ToArray();
    }

    public async Task<IReadOnlyCollection<IngressConfiguration>> GetRollbackConfigurationAsync(
        string artifactId, long ingressGeneration, IReadOnlyCollection<string> triggerKeys, CancellationToken cancellationToken = default)
    {
        if (triggerKeys is null || triggerKeys.Count == 0) return [];
        var runtimeArtifactId = new Id(Ulid.Parse(artifactId));
        var rows = await _dbContext.RuntimeIngressConfigurations.AsNoTracking()
            .Join(_dbContext.RuntimeOrchestrationArtifacts.AsNoTracking(),
                configuration => configuration.RuntimeOrchestrationArtifactId, artifact => artifact.Id,
                (configuration, artifact) => new { Configuration = configuration, Artifact = artifact })
            .Where(x => x.Configuration.RuntimeOrchestrationArtifactId == runtimeArtifactId &&
                x.Artifact.IsActive && x.Artifact.Status == RuntimeOrchestrationArtifactStatus.Ready &&
                x.Artifact.LifecycleStatus == RuntimeOrchestrationLifecycleStatus.Active &&
                x.Artifact.IngressGeneration == ingressGeneration &&
                ((x.Configuration.IngressKind == IngressKind.Trigger &&
                  x.Configuration.MessageDisposition == IngressMessageDisposition.Process && triggerKeys.Contains(x.Configuration.ConfigurationKey)) ||
                 (x.Configuration.IngressKind == IngressKind.Backchannel && x.Configuration.IsActive)))
            .OrderBy(x => x.Configuration.ConfigurationKey).ToArrayAsync(cancellationToken);
        return rows.Select(x => new IngressConfiguration
        {
            Id = x.Configuration.Id.ToString(), ArtifactId = x.Artifact.Id.ToString(), IsStaged = true,
            OrchestrationDefinitionKey = x.Artifact.OrchestrationDefinitionKey,
            OrchestrationVersion = x.Artifact.Version.ToString(), DeployedOnUtc = x.Artifact.DeployedOnUtc,
            IngressKind = x.Configuration.IngressKind, MessageDisposition = x.Configuration.MessageDisposition,
            IngressTransport = x.Configuration.IngressTransport, SettingsPayload = x.Configuration.SettingsPayload
        }).ToArray();
    }

    private async Task<IReadOnlyCollection<IngressConfiguration>> GetConfigurationCoreAsync(
        string artifactId, long? stagedGeneration, CancellationToken cancellationToken)
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
                (x.Artifact.Status == RuntimeOrchestrationArtifactStatus.Ready ||
                 (stagedGeneration.HasValue && (x.Artifact.Status == RuntimeOrchestrationArtifactStatus.Pending ||
                                                x.Artifact.Status == RuntimeOrchestrationArtifactStatus.Failed))) &&
                (!stagedGeneration.HasValue || x.Artifact.IngressGeneration == stagedGeneration.Value))
            .OrderBy(x => x.Configuration.ConfigurationKey)
            .ToArrayAsync(cancellationToken);

        return rows
            .Select(x => new IngressConfiguration
            {
                Id = x.Configuration.Id.ToString(),
                ArtifactId = x.Configuration.RuntimeOrchestrationArtifactId.ToString(),
                IsStaged = stagedGeneration.HasValue,
                OrchestrationDefinitionKey = x.Artifact.OrchestrationDefinitionKey,
                OrchestrationVersion = x.Artifact.Version.ToString(),
                DeployedOnUtc = x.Artifact.DeployedOnUtc,
                IngressKind = x.Configuration.IngressKind,
                MessageDisposition = x.Configuration.MessageDisposition,
                IngressTransport = x.Configuration.IngressTransport,
                SettingsPayload = x.Configuration.SettingsPayload
            })
            .ToArray();
    }

    public void Dispose()
    {
    }
}
