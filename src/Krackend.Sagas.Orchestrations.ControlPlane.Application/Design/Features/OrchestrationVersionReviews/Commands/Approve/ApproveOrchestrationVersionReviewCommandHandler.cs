namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;
using Pelican.Mediator;

/// <summary>Approves a pending review while leaving the version frozen until Apply.</summary>
public sealed class ApproveOrchestrationVersionReviewCommandHandler : IRequestHandler<ApproveOrchestrationVersionReviewCommand, bool>
{
    private readonly IOrchestrationVersionReviewRepository _reviewRepository;

    /// <summary>Initializes a new instance.</summary>
    public ApproveOrchestrationVersionReviewCommandHandler(IOrchestrationVersionReviewRepository reviewRepository)
    {
        _reviewRepository = reviewRepository ?? throw new ArgumentNullException(nameof(reviewRepository));
    }

    /// <inheritdoc />
    public async Task<bool> Handle(ApproveOrchestrationVersionReviewCommand request, CancellationToken cancellationToken)
    {
        var review = await _reviewRepository.GetById(PrimitiveParser.ParseId(request.ReviewId), cancellationToken);
        if (review.ApprovalStatus != OrchestrationVersionReviewApprovalStatus.Pending ||
            review.ApplicationStatus != OrchestrationVersionReviewApplicationStatus.Pending)
        {
            throw new InvalidOperationException("Only pending reviews can be approved.");
        }

        review.ApprovalStatus = OrchestrationVersionReviewApprovalStatus.Approved;
        review.DecidedBy = request.ApprovedBy;
        review.DecidedOnUtc = DateTime.UtcNow;
        await _reviewRepository.Update(review, cancellationToken);
        return true;
    }
}
