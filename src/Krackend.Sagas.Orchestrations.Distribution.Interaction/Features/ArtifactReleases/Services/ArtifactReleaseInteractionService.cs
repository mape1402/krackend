using Krackend.Sagas.Orchestrations.Design.Storage;
using Krackend.Sagas.Orchestrations.Distribution.Storage;

namespace Krackend.Sagas.Orchestrations.Distribution.Interaction;

public sealed class ArtifactInteractionService : IArtifactInteractionService
{
    private readonly IArtifactRepository _repository;

    public ArtifactInteractionService(IArtifactRepository repository) { _repository = repository; }

    public async Task<InteractionPagedResult<ArtifactModel>> GetAll(InteractionPagedSettings settings, CancellationToken cancellationToken = default)
    {
        var result = await _repository.GetAll(new PagedSettings(settings.PageNumber, settings.PageSize, Array.Empty<QueryFilter>(), Array.Empty<QuerySort>()), cancellationToken);
        return new InteractionPagedResult<ArtifactModel>
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

