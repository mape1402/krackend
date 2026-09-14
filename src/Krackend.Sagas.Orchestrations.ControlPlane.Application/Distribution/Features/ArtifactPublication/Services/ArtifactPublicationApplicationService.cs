using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Contracts.Events;
using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Storage;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Distribution;

/// <summary>
/// Creates distribution artifacts from design lifecycle operations.
/// </summary>
public sealed class ArtifactPublicationApplicationService : IArtifactPublicationApplicationService
{
    private readonly IArtifactBuilder<OrchestrationVersionDeployedEvent> _deploymentBuilder;
    private readonly IArtifactBuilder<OrchestrationVersionDeprecatedEvent> _deprecationBuilder;
    private readonly IArtifactBuilder<OrchestrationVersionArchivedEvent> _archiveBuilder;
    private readonly IEnumerable<IArtifactValidationPolicy> _policies;
    private readonly IArtifactRepository _artifactRepository;

    /// <summary>
    /// Initializes a new instance of the <see cref="ArtifactPublicationApplicationService"/> class.
    /// </summary>
    public ArtifactPublicationApplicationService(
        IArtifactBuilder<OrchestrationVersionDeployedEvent> deploymentBuilder,
        IArtifactBuilder<OrchestrationVersionDeprecatedEvent> deprecationBuilder,
        IArtifactBuilder<OrchestrationVersionArchivedEvent> archiveBuilder,
        IEnumerable<IArtifactValidationPolicy> policies,
        IArtifactRepository artifactRepository)
    {
        _deploymentBuilder = deploymentBuilder ?? throw new ArgumentNullException(nameof(deploymentBuilder));
        _deprecationBuilder = deprecationBuilder ?? throw new ArgumentNullException(nameof(deprecationBuilder));
        _archiveBuilder = archiveBuilder ?? throw new ArgumentNullException(nameof(archiveBuilder));
        _policies = policies ?? throw new ArgumentNullException(nameof(policies));
        _artifactRepository = artifactRepository ?? throw new ArgumentNullException(nameof(artifactRepository));
    }

    /// <inheritdoc />
    public async Task PublishDeployment(OrchestrationVersionDeployedEvent deployment, CancellationToken cancellationToken = default)
    {
        await GetOrCreateArtifact(_deploymentBuilder.Build(deployment), cancellationToken);
    }

    /// <inheritdoc />
    public async Task PublishDeprecation(OrchestrationVersionDeprecatedEvent deprecation, CancellationToken cancellationToken = default)
    {
        await GetOrCreateArtifact(_deprecationBuilder.Build(deprecation), cancellationToken);
    }

    /// <inheritdoc />
    public async Task PublishArchive(OrchestrationVersionArchivedEvent archive, CancellationToken cancellationToken = default)
    {
        await GetOrCreateArtifact(_archiveBuilder.Build(archive), cancellationToken);
    }

    private async Task<Artifact> GetOrCreateArtifact(Artifact candidate, CancellationToken cancellationToken)
    {
        Validate(candidate);

        var existing = await _artifactRepository.GetLatestForOrchestrationVersion(
            new Id(Ulid.Parse(candidate.OrchestrationVersionId)),
            candidate.ArtifactType,
            cancellationToken);

        if (existing is not null && string.Equals(existing.Checksum, candidate.Checksum, StringComparison.Ordinal))
        {
            return existing;
        }

        await _artifactRepository.Create(candidate, cancellationToken);
        return candidate;
    }

    private void Validate(Artifact artifact)
    {
        foreach (var policy in _policies.Where(x => x.CanValidate(artifact.ArtifactType)))
        {
            policy.Validate(artifact);
        }
    }

}
