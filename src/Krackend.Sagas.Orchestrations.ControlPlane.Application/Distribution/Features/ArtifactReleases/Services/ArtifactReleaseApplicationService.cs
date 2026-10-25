using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Storage;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Distribution;

public sealed class ArtifactApplicationService : IArtifactApplicationService
{
    private readonly IArtifactRepository _repository;

    public ArtifactApplicationService(IArtifactRepository repository) { _repository = repository; }

    public async Task<ApplicationPagedResult<ArtifactModel>> GetAll(ApplicationPagedSettings settings, CancellationToken cancellationToken = default)
    {
        var result = await _repository.GetAll(new PagedSettings(settings.PageNumber, settings.PageSize, Array.Empty<QueryFilter>(), Array.Empty<QuerySort>()), cancellationToken);
        return MapPage(result);
    }

    public async Task<ApplicationPagedResult<ArtifactModel>> Search(ApplicationPagedSettings settings, string orchestrationId, string search, string sort, bool descending, CancellationToken cancellationToken = default)
        => MapPage(await _repository.Search(new PagedSettings(settings.PageNumber, settings.PageSize, [], []), orchestrationId, search, sort, descending, cancellationToken));

    public async Task<ArtifactModel> GetById(string artifactId, CancellationToken cancellationToken = default)
    {
        var artifact = await _repository.GetById(new Id(Ulid.Parse(artifactId)), cancellationToken);
        return artifact is null ? null : Map(artifact);
    }

    public async Task<IReadOnlyCollection<ArtifactModel>> GetSummariesByIds(IReadOnlyCollection<string> artifactIds, CancellationToken cancellationToken = default)
    {
        var ids = artifactIds.Select(x => new Id(Ulid.Parse(x))).Distinct().ToArray();
        return (await _repository.GetSummariesByIds(ids, cancellationToken)).Select(Map).ToArray();
    }

    private static ApplicationPagedResult<ArtifactModel> MapPage(PagedResult<Artifact> result)
    {
        return new ApplicationPagedResult<ArtifactModel>
        {
            PageNumber = result.PageNumber,
            PageSize = result.PageSize,
            TotalRows = (int)result.TotalRows,
            TotalPages = result.TotalPages,
            Rows = result.Rows.Select(Map).ToArray()
        };
    }

    private static ArtifactModel Map(Artifact x) => new()
            {
                Id = x.Id.ToString(),
                OrchestrationDefinitionId = x.OrchestrationDefinitionId,
                OrchestrationVersionId = x.OrchestrationVersionId,
                OrchestrationDisplayName = x.OrchestrationDisplayName,
                VersionLabel = x.VersionLabel,
                VersionNumber = x.VersionNumber,
                ArtifactType = x.ArtifactType,
                SchemaVersion = x.SchemaVersion,
                Payload = x.Payload,
                Metadata = x.Metadata,
                SourceEvent = x.SourceEvent,
                SourceVersion = x.SourceVersion,
                Checksum = x.Checksum,
                IsPublished = x.IsPublished,
                CreatedAtUtc = x.CreatedAtUtc,
                PublishedAtUtc = x.PublishedAtUtc
            };
}

