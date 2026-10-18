namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

using System.Security.Cryptography;
using System.Text;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Contracts.Events;
using Krackend.Sagas.Orchestrations.ControlPlane.Application.Distribution;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;
using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Storage;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage;
using Pelican.Mediator;

/// <summary>
/// Applies an approved review, atomically producing the correct deployed artifact and final version state.
/// </summary>
public sealed class ApplyOrchestrationVersionReviewCommandHandler : IRequestHandler<ApplyOrchestrationVersionReviewCommand, bool>
{
    private readonly IOrchestrationVersionReviewRepository _reviewRepository;
    private readonly IOrchestrationVersionRepository _versionRepository;
    private readonly IOrchestrationDefinitionRepository _definitionRepository;
    private readonly IOrchestrationVersionTransitionPolicy _transitionPolicy;
    private readonly IOrchestrationVersionArtifactSnapshotBuilder _snapshotBuilder;
    private readonly IOrchestrationArtifactPayloadFactory _payloadFactory;
    private readonly IOrchestrationArtifactDslValidationService _dslValidationService;
    private readonly IArtifactPublicationApplicationService _artifactPublicationService;
    private readonly IArtifactRepository _artifactRepository;
    private readonly IControlPlaneUnitOfWork _unitOfWork;

    /// <summary>Initializes a new instance.</summary>
    public ApplyOrchestrationVersionReviewCommandHandler(
        IOrchestrationVersionReviewRepository reviewRepository,
        IOrchestrationVersionRepository versionRepository,
        IOrchestrationDefinitionRepository definitionRepository,
        IOrchestrationVersionTransitionPolicy transitionPolicy,
        IOrchestrationVersionArtifactSnapshotBuilder snapshotBuilder,
        IOrchestrationArtifactPayloadFactory payloadFactory,
        IOrchestrationArtifactDslValidationService dslValidationService,
        IArtifactPublicationApplicationService artifactPublicationService,
        IArtifactRepository artifactRepository,
        IControlPlaneUnitOfWork unitOfWork)
    {
        _reviewRepository = reviewRepository ?? throw new ArgumentNullException(nameof(reviewRepository));
        _versionRepository = versionRepository ?? throw new ArgumentNullException(nameof(versionRepository));
        _definitionRepository = definitionRepository ?? throw new ArgumentNullException(nameof(definitionRepository));
        _transitionPolicy = transitionPolicy ?? throw new ArgumentNullException(nameof(transitionPolicy));
        _snapshotBuilder = snapshotBuilder ?? throw new ArgumentNullException(nameof(snapshotBuilder));
        _payloadFactory = payloadFactory ?? throw new ArgumentNullException(nameof(payloadFactory));
        _dslValidationService = dslValidationService ?? throw new ArgumentNullException(nameof(dslValidationService));
        _artifactPublicationService = artifactPublicationService ?? throw new ArgumentNullException(nameof(artifactPublicationService));
        _artifactRepository = artifactRepository ?? throw new ArgumentNullException(nameof(artifactRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    /// <inheritdoc />
    public async Task<bool> Handle(ApplyOrchestrationVersionReviewCommand request, CancellationToken cancellationToken)
    {
        var review = await _reviewRepository.GetById(PrimitiveParser.ParseId(request.ReviewId), cancellationToken);
        if (review.ApprovalStatus != OrchestrationVersionReviewApprovalStatus.Approved ||
            review.ApplicationStatus != OrchestrationVersionReviewApplicationStatus.Pending)
        {
            throw new InvalidOperationException("Only approved reviews awaiting application can be applied.");
        }

        var version = await _versionRepository.GetById(review.OrchestrationVersionId, cancellationToken);
        if (version.Status != review.PendingStatus || !string.Equals(version.Checksum.Value, review.ExpectedChecksum, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The approved review no longer matches the orchestration version snapshot.");
        }

        if (review.Operation == OrchestrationVersionReviewOperation.Deploy)
        {
            if (string.IsNullOrWhiteSpace(request.Version) || !ValidationRules.IsSemanticVersion(request.Version))
            {
                throw new InvalidOperationException("A semantic version is required when applying a deployment review.");
            }

            var semanticVersion = PrimitiveParser.ParseSemanticVersion(request.Version);
            if (await _versionRepository.Exists(version.OrchestrationDefinitionId, semanticVersion, cancellationToken))
            {
                throw new InvalidOperationException($"Orchestration version '{semanticVersion}' already exists for this orchestration.");
            }

            version.Version = semanticVersion;
            version.VersionLabel = request.VersionLabel?.Trim() ?? string.Empty;
            version.Description = request.Description?.Trim() ?? string.Empty;
            version.Notes = request.Notes?.Trim() ?? string.Empty;
        }

        if (!version.Version.HasValue)
        {
            throw new InvalidOperationException("A deployed, deprecated, or archived orchestration version must have a semantic version.");
        }

        _transitionPolicy.EnsureCanTransition(version.Status, review.TargetStatus);
        var definition = await _definitionRepository.GetById(version.OrchestrationDefinitionId, cancellationToken);
        var snapshot = await _snapshotBuilder.Build(version, cancellationToken);
        _dslValidationService.Validate(snapshot);
        var payload = _payloadFactory.CreatePayloadJson(definition, snapshot);
        version.Checksum = CreateChecksum(payload);

        await _unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            version.Status = review.TargetStatus;
            version.UpdatedBy = request.AppliedBy;
            version.UpdatedOnUtc = DateTime.UtcNow;
            await _versionRepository.Update(version, token);

            await Publish(review.Operation, definition, version, payload, request.AppliedBy, token);
            var artifact = await _artifactRepository.GetLatestForOrchestrationVersion(
                version.Id,
                GetArtifactType(review.Operation),
                token);

            review.ApplicationStatus = OrchestrationVersionReviewApplicationStatus.Applied;
            review.AppliedBy = request.AppliedBy;
            review.AppliedOnUtc = DateTime.UtcNow;
            review.DeployedArtifactId = artifact?.Id.ToString() ?? string.Empty;
            await _reviewRepository.Update(review, token);
        }, cancellationToken);

        return true;
    }

    private Task Publish(
        OrchestrationVersionReviewOperation operation,
        OrchestrationDefinition definition,
        OrchestrationVersion version,
        string payload,
        string actor,
        CancellationToken cancellationToken)
    {
        var occurredAt = DateTime.UtcNow;
        return operation switch
        {
            OrchestrationVersionReviewOperation.Deploy => _artifactPublicationService.PublishDeployment(new OrchestrationVersionDeployedEvent(
                version.Id.ToString(), version.OrchestrationDefinitionId.ToString(), definition.Name, version.VersionLabel,
                version.Version!.Value.ToString(), payload, version.Checksum.Value, actor, version.Id.ToString(), occurredAt), cancellationToken),
            OrchestrationVersionReviewOperation.Deprecate => _artifactPublicationService.PublishDeprecation(new OrchestrationVersionDeprecatedEvent(
                version.Id.ToString(), version.OrchestrationDefinitionId.ToString(), definition.Name, version.VersionLabel,
                version.Version!.Value.ToString(), payload, version.Checksum.Value, actor, version.Id.ToString(), occurredAt), cancellationToken),
            OrchestrationVersionReviewOperation.Archive => _artifactPublicationService.PublishArchive(new OrchestrationVersionArchivedEvent(
                version.Id.ToString(), version.OrchestrationDefinitionId.ToString(), definition.Name, version.VersionLabel,
                version.Version!.Value.ToString(), payload, version.Checksum.Value, actor, version.Id.ToString(), occurredAt), cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, "Unsupported review operation."),
        };
    }

    private static string GetArtifactType(OrchestrationVersionReviewOperation operation)
        => operation switch
        {
            OrchestrationVersionReviewOperation.Deploy => ArtifactTypes.NewOrchestrationVersion,
            OrchestrationVersionReviewOperation.Deprecate => ArtifactTypes.DeprecatedOrchestrationVersion,
            OrchestrationVersionReviewOperation.Archive => ArtifactTypes.ArchivedOrchestrationVersion,
            _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, "Unsupported review operation."),
        };

    private static Checksum CreateChecksum(string payload)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return new Checksum($"sha256:{Convert.ToHexString(hash).ToLowerInvariant()}");
    }
}
