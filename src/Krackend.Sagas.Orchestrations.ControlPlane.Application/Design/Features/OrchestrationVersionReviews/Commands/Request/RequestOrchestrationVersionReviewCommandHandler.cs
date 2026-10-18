namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage;
using Pelican.Mediator;

/// <summary>
/// Creates a review request and freezes the version in its corresponding pending state.
/// </summary>
public sealed class RequestOrchestrationVersionReviewCommandHandler : IRequestHandler<RequestOrchestrationVersionReviewCommand, string>
{
    private readonly IOrchestrationVersionRepository _versionRepository;
    private readonly IOrchestrationVersionReviewRepository _reviewRepository;
    private readonly IOrchestrationVersionTransitionPolicy _transitionPolicy;
    private readonly IControlPlaneUnitOfWork _unitOfWork;

    /// <summary>Initializes a new instance.</summary>
    public RequestOrchestrationVersionReviewCommandHandler(
        IOrchestrationVersionRepository versionRepository,
        IOrchestrationVersionReviewRepository reviewRepository,
        IOrchestrationVersionTransitionPolicy transitionPolicy,
        IControlPlaneUnitOfWork unitOfWork)
    {
        _versionRepository = versionRepository ?? throw new ArgumentNullException(nameof(versionRepository));
        _reviewRepository = reviewRepository ?? throw new ArgumentNullException(nameof(reviewRepository));
        _transitionPolicy = transitionPolicy ?? throw new ArgumentNullException(nameof(transitionPolicy));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    /// <inheritdoc />
    public async Task<string> Handle(RequestOrchestrationVersionReviewCommand request, CancellationToken cancellationToken)
    {
        var version = await _versionRepository.GetById(PrimitiveParser.ParseId(request.OrchestrationVersionId), cancellationToken);
        var existing = await _reviewRepository.GetPendingForVersion(version.Id, cancellationToken);
        if (existing is not null)
        {
            throw new InvalidOperationException($"Orchestration version '{version.Id}' already has a review awaiting application.");
        }

        var transition = ResolveTransition(request.Operation);
        if (version.Status != transition.From)
        {
            throw new InvalidOperationException($"Cannot request '{request.Operation}' while the version is '{version.Status}'.");
        }

        _transitionPolicy.EnsureCanTransition(version.Status, transition.Pending);
        var review = new OrchestrationVersionReview
        {
            Id = Id.New(),
            OrchestrationVersionId = version.Id,
            Operation = request.Operation,
            FromStatus = transition.From,
            PendingStatus = transition.Pending,
            TargetStatus = transition.Target,
            ExpectedChecksum = version.Checksum.Value,
            RequestedBy = request.RequestedBy,
            RequestedOnUtc = DateTime.UtcNow,
        };

        await _unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            version.Status = transition.Pending;
            version.UpdatedOnUtc = DateTime.UtcNow;
            version.UpdatedBy = request.RequestedBy;
            await _versionRepository.Update(version, token);
            await _reviewRepository.Create(review, token);
        }, cancellationToken);

        return review.Id.ToString();
    }

    internal static (OrchestrationVersionStatus From, OrchestrationVersionStatus Pending, OrchestrationVersionStatus Target) ResolveTransition(
        OrchestrationVersionReviewOperation operation)
        => operation switch
        {
            OrchestrationVersionReviewOperation.Deploy => (
                OrchestrationVersionStatus.Draft,
                OrchestrationVersionStatus.PendingDeployment,
                OrchestrationVersionStatus.Deployed),
            OrchestrationVersionReviewOperation.Deprecate => (
                OrchestrationVersionStatus.Deployed,
                OrchestrationVersionStatus.PendingDeprecation,
                OrchestrationVersionStatus.Deprecated),
            OrchestrationVersionReviewOperation.Archive => (
                OrchestrationVersionStatus.Deprecated,
                OrchestrationVersionStatus.PendingArchive,
                OrchestrationVersionStatus.Archived),
            _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, "Unsupported orchestration version review operation."),
        };
}
