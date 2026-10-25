namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Distribution;

public interface IArtifactApplicationService
{
    Task<ApplicationPagedResult<ArtifactModel>> GetAll(ApplicationPagedSettings settings, CancellationToken cancellationToken = default);
    /// <summary>Searches artifact summaries.</summary>
    Task<ApplicationPagedResult<ArtifactModel>> Search(ApplicationPagedSettings settings, string orchestrationId, string search, string sort, bool descending, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("The configured artifact service does not support catalog search.");
    /// <summary>Gets the full artifact including its payload.</summary>
    Task<ArtifactModel> GetById(string artifactId, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("The configured artifact service does not support direct lookup.");
    Task<IReadOnlyCollection<ArtifactModel>> GetSummariesByIds(IReadOnlyCollection<string> artifactIds, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("The configured artifact service does not support summary lookup.");
}

