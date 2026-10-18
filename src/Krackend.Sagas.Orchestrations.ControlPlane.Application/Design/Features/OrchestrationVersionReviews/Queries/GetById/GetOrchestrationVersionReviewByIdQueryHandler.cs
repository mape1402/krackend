namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;
using Pelican.Mediator;

/// <summary>Handles a request for one lifecycle review.</summary>
public sealed class GetOrchestrationVersionReviewByIdQueryHandler
    : IRequestHandler<GetOrchestrationVersionReviewByIdQuery, OrchestrationVersionReviewModel>
{
    private readonly IOrchestrationVersionReviewRepository _repository;

    public GetOrchestrationVersionReviewByIdQueryHandler(IOrchestrationVersionReviewRepository repository)
        => _repository = repository ?? throw new ArgumentNullException(nameof(repository));

    public async Task<OrchestrationVersionReviewModel> Handle(
        GetOrchestrationVersionReviewByIdQuery request,
        CancellationToken cancellationToken)
        => Map(await _repository.GetById(PrimitiveParser.ParseId(request.ReviewId), cancellationToken));

    internal static OrchestrationVersionReviewModel Map(OrchestrationVersionReview review)
        => new()
        {
            Id = review.Id.ToString(),
            OrchestrationVersionId = review.OrchestrationVersionId.ToString(),
            Operation = review.Operation,
            FromStatus = review.FromStatus,
            PendingStatus = review.PendingStatus,
            TargetStatus = review.TargetStatus,
            ApprovalStatus = review.ApprovalStatus,
            ApplicationStatus = review.ApplicationStatus,
            RequestedBy = review.RequestedBy,
            RequestedOnUtc = review.RequestedOnUtc,
            DecidedBy = review.DecidedBy,
            DecidedOnUtc = review.DecidedOnUtc,
            RejectionComment = review.RejectionComment,
            AbortedBy = review.AbortedBy,
            AbortedOnUtc = review.AbortedOnUtc,
            AbortReason = review.AbortReason,
            AppliedBy = review.AppliedBy,
            AppliedOnUtc = review.AppliedOnUtc,
            DeployedArtifactId = review.DeployedArtifactId,
        };
}
