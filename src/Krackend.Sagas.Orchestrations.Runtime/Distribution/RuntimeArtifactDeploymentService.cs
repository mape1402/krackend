using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Distribution;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;

namespace Krackend.Sagas.Orchestrations.Runtime.Distribution;

/// <summary>
/// Default runtime artifact installer.
/// </summary>
public sealed class RuntimeArtifactDeploymentService : IRuntimeArtifactDeploymentService
{
    private readonly IRuntimeArtifactRepository _artifactRepository;
    private readonly IRuntimeStorageUnitOfWork _unitOfWork;
    private readonly IRuntimeArtifactProjectionScheduler _projectionScheduler;

    /// <summary>
    /// Initializes a new instance of the <see cref="RuntimeArtifactDeploymentService"/> class.
    /// </summary>
    public RuntimeArtifactDeploymentService(
        IRuntimeArtifactRepository artifactRepository,
        IRuntimeStorageUnitOfWork unitOfWork,
        IRuntimeArtifactProjectionScheduler projectionScheduler)
    {
        _artifactRepository = artifactRepository ?? throw new ArgumentNullException(nameof(artifactRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _projectionScheduler = projectionScheduler ?? throw new ArgumentNullException(nameof(projectionScheduler));
    }

    /// <inheritdoc />
    public async Task<RuntimeArtifactDeploymentResult> DeployAsync(
        RuntimeArtifactDeliveryPackage package,
        string sourceKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(package);

        var version = ParseVersion(package.Version);
        var artifactId = ParseId(package.ArtifactId);
        var existingArtifact = await TryGetExistingArtifact(
            package.EnvironmentKey,
            package.OrchestrationDefinitionKey,
            version,
            cancellationToken);
        if (existingArtifact is not null &&
            existingArtifact.ArtifactChecksum.Value == package.Checksum &&
            existingArtifact.Status == RuntimeOrchestrationArtifactStatus.Ready)
        {
            return new RuntimeArtifactDeploymentResult
            {
                Accepted = true,
                RuntimeArtifactId = existingArtifact.Id.ToString(),
                Status = RuntimeOrchestrationArtifactStatus.Ready.ToString(),
                Message = $"Artifact '{existingArtifact.OrchestrationDefinitionKey}' v{existingArtifact.Version} is already ready."
            };
        }

        var now = DateTime.UtcNow;
        var ingressGeneration = existingArtifact?.ArtifactChecksum.Value == package.Checksum
            ? Math.Max(existingArtifact.IngressGeneration, 1)
            : (existingArtifact?.IngressGeneration ?? 0) + 1;
        var artifact = new RuntimeOrchestrationArtifact
        {
            Id = existingArtifact?.Id ?? artifactId,
            EnvironmentKey = package.EnvironmentKey,
            OrchestrationDefinitionKey = package.OrchestrationDefinitionKey,
            ArtifactType = package.ArtifactType,
            SourceOrchestrationVersionId = ParseId(package.OrchestrationVersionId),
            Version = version,
            ArtifactChecksum = new Checksum(package.Checksum),
            ArtifactPayload = JsonNode.Parse(package.PayloadJson)
                ?? throw new InvalidOperationException("Artifact payload is empty."),
            Status = RuntimeOrchestrationArtifactStatus.Pending,
            IngressGeneration = ingressGeneration,
            IsActive = true,
            LoadedToCache = false,
            DeployedOnUtc = existingArtifact?.DeployedOnUtc ?? now,
            ActivatedOnUtc = null,
            ProjectionStartedOnUtc = null,
            ProjectionCompletedOnUtc = null,
            ProjectionFailedOnUtc = null,
            ProjectionError = null,
            RetiredOnUtc = null,
            SupersededByArtifactId = null,
            Notes = $"Installed from '{sourceKey}' release target '{package.ReleaseTargetId}'."
        };

        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);
        using (_unitOfWork.DeferAutoSave())
        {
            await _artifactRepository.Upsert(artifact, cancellationToken);
            await _projectionScheduler.ScheduleProjectionAsync(new RuntimeArtifactProjectionRequest
            {
                ArtifactId = artifact.Id.ToString(),
                IngressGeneration = artifact.IngressGeneration,
                RequestedBy = sourceKey,
                RequestedOnUtc = now
            }, cancellationToken);
            await _unitOfWork.SaveChanges(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);

        return new RuntimeArtifactDeploymentResult
        {
            Accepted = true,
            RuntimeArtifactId = artifact.Id.ToString(),
            Status = RuntimeOrchestrationArtifactStatus.Pending.ToString(),
            Message = $"Artifact '{artifact.OrchestrationDefinitionKey}' v{artifact.Version} accepted for ingress projection."
        };
    }

    private async Task<RuntimeOrchestrationArtifact> TryGetExistingArtifact(
        string environmentKey,
        string orchestrationDefinitionKey,
        SemanticVersion version,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _artifactRepository.GetByVersion(
                environmentKey,
                orchestrationDefinitionKey,
                version,
                cancellationToken);
        }
        catch (KeyNotFoundException)
        {
            return null;
        }
    }

    private static Id ParseId(string value)
        => new(Ulid.Parse(value));

    private static SemanticVersion ParseVersion(string value)
    {
        var parts = (value ?? string.Empty).Split('.');
        if (parts.Length != 3
            || !int.TryParse(parts[0], out var major)
            || !int.TryParse(parts[1], out var minor)
            || !int.TryParse(parts[2], out var patch))
        {
            throw new InvalidOperationException($"Semantic version '{value}' is invalid.");
        }

        return new SemanticVersion(major, minor, patch);
    }
}
