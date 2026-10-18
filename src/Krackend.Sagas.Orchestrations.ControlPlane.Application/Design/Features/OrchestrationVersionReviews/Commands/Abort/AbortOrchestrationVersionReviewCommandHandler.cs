namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage;
using Pelican.Mediator;

/// <summary>Aborts a review and restores the original lifecycle state.</summary>
public sealed class AbortOrchestrationVersionReviewCommandHandler : IRequestHandler<AbortOrchestrationVersionReviewCommand, bool>
{
    private readonly IOrchestrationVersionReviewRepository _reviewRepository;
    private readonly IOrchestrationVersionRepository _versionRepository;
    private readonly IControlPlaneUnitOfWork _unitOfWork;

    /// <summary>Initializes a new instance.</summary>
    public AbortOrchestrationVersionReviewCommandHandler(
        IOrchestrationVersionReviewRepository reviewRepository,
        IOrchestrationVersionRepository versionRepository,
        IControlPlaneUnitOfWork unitOfWork)
    {
        _reviewRepository = reviewRepository ?? throw new ArgumentNullException(nameof(reviewRepository));
        _versionRepository = versionRepository ?? throw new ArgumentNullException(nameof(versionRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    /// <inheritdoc />
    public async Task<bool> Handle(AbortOrchestrationVersionReviewCommand request, CancellationToken cancellationToken)
    {
        var review = await _reviewRepository.GetById(PrimitiveParser.ParseId(request.ReviewId), cancellationToken);
        if (review.ApplicationStatus != OrchestrationVersionReviewApplicationStatus.Pending ||
            review.ApprovalStatus == OrchestrationVersionReviewApprovalStatus.Rejected)
        {
            throw new InvalidOperationException("Only pending or approved reviews can be aborted.");
        }

        var version = await _versionRepository.GetById(review.OrchestrationVersionId, cancellationToken);
        if (version.Status != review.PendingStatus)
        {
            throw new InvalidOperationException("The orchestration version no longer matches the review pending state.");
        }

        await _unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            review.ApplicationStatus = OrchestrationVersionReviewApplicationStatus.Aborted;
            review.AbortedBy = request.AbortedBy;
            review.AbortedOnUtc = DateTime.UtcNow;
            review.AbortReason = request.Reason?.Trim() ?? string.Empty;
            await _reviewRepository.Update(review, token);

            version.Status = review.FromStatus;
            version.UpdatedBy = request.AbortedBy;
            version.UpdatedOnUtc = DateTime.UtcNow;
            await _versionRepository.Update(version, token);
        }, cancellationToken);

        return true;
    }
}
