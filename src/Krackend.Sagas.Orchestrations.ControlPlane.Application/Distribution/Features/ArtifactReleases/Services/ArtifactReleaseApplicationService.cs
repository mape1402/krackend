using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;
using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Storage;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Distribution;

public sealed class ArtifactApplicationService : IArtifactApplicationService
{
    private readonly IArtifactRepository _repository;

    public ArtifactApplicationService(IArtifactRepository repository) { _repository = repository; }

    public async Task<ApplicationPagedResult<ArtifactModel>> GetAll(ApplicationPagedSettings settings, CancellationToken cancellationToken = default)
    {
        var result = await _repository.GetAll(new PagedSettings(settings.PageNumber, settings.PageSize, Array.Empty<QueryFilter>(), Array.Empty<QuerySort>()), cancellationToken);
        return new ApplicationPagedResult<ArtifactModel>
        {
            PageNumber = result.PageNumber,
            PageSize = result.PageSize,
            TotalRows = (int)result.TotalRows,
            TotalPages = result.TotalPages,
            Rows = result.Rows.Select(x => new ArtifactModel
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
            }).ToArray()
        };
    }
}

