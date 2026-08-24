using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
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
        var configurations = await _dbContext.RuntimeIngressConfigurations.AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.RuntimeOrchestrationArtifactId)
            .ThenBy(x => x.ConfigurationKey)
            .Skip(_skip)
            .Take(PageSize)
            .Select(x => new IngressConfiguration
            {
                Id = x.Id.ToString(),
                ArtifactId = x.RuntimeOrchestrationArtifactId.ToString(),
                IngressKind = x.IngressKind,
                IngressTransport = x.IngressTransport,
                SettingsPayload = x.SettingsPayload
            })
            .ToArrayAsync(cancellationToken);

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
        return await _dbContext.RuntimeIngressConfigurations.AsNoTracking()
            .Where(x => x.RuntimeOrchestrationArtifactId == runtimeArtifactId && x.IsActive)
            .OrderBy(x => x.ConfigurationKey)
            .Select(x => new IngressConfiguration
            {
                Id = x.Id.ToString(),
                ArtifactId = x.RuntimeOrchestrationArtifactId.ToString(),
                IngressKind = x.IngressKind,
                IngressTransport = x.IngressTransport,
                SettingsPayload = x.SettingsPayload
            })
            .ToArrayAsync(cancellationToken);
    }

    public void Dispose()
    {
    }
}
