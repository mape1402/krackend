namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage;
using Pelican.Mediator;

/// <summary>Rejects a review and atomically restores the prior version state.</summary>
public sealed class RejectOrchestrationVersionReviewCommandHandler : IRequestHandler<RejectOrchestrationVersionReviewCommand, bool>
{
    private readonly IOrchestrationVersionReviewRepository _reviewRepository;
    private readonly IOrchestrationVersionRepository _versionRepository;
    private readonly IControlPlaneUnitOfWork _unitOfWork;

    /// <summary>Initializes a new instance.</summary>
    public RejectOrchestrationVersionReviewCommandHandler(
        IOrchestrationVersionReviewRepository reviewRepository,
        IOrchestrationVersionRepository versionRepository,
        IControlPlaneUnitOfWork unitOfWork)
    {
        _reviewRepository = reviewRepository ?? throw new ArgumentNullException(nameof(reviewRepository));
        _versionRepository = versionRepository ?? throw new ArgumentNullException(nameof(versionRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    /// <inheritdoc />
    public async Task<bool> Handle(RejectOrchestrationVersionReviewCommand request, CancellationToken cancellationToken)
    {
        var review = await _reviewRepository.GetById(PrimitiveParser.ParseId(request.ReviewId), cancellationToken);
        if (review.ApprovalStatus != OrchestrationVersionReviewApprovalStatus.Pending ||
            review.ApplicationStatus != OrchestrationVersionReviewApplicationStatus.Pending)
        {
            throw new InvalidOperationException("Only pending reviews can be rejected.");
        }

        var version = await _versionRepository.GetById(review.OrchestrationVersionId, cancellationToken);
        if (version.Status != review.PendingStatus)
        {
            throw new InvalidOperationException("The orchestration version no longer matches the review pending state.");
        }

        await _unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            review.ApprovalStatus = OrchestrationVersionReviewApprovalStatus.Rejected;
            review.ApplicationStatus = OrchestrationVersionReviewApplicationStatus.Aborted;
            review.DecidedBy = request.RejectedBy;
            review.DecidedOnUtc = DateTime.UtcNow;
            review.RejectionComment = request.Comment?.Trim() ?? string.Empty;
            await _reviewRepository.Update(review, token);

            version.Status = review.FromStatus;
            version.UpdatedBy = request.RejectedBy;
            version.UpdatedOnUtc = DateTime.UtcNow;
            await _versionRepository.Update(version, token);
        }, cancellationToken);

        return true;
    }
}
