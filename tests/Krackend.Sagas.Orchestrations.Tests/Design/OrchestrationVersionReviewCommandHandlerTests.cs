using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;
using Krackend.Sagas.Orchestrations.ControlPlane.Application.Distribution;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;
using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Storage;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage;
using NSubstitute;

namespace Krackend.Sagas.Orchestrations.Tests.Design;

public sealed class OrchestrationVersionReviewCommandHandlerTests
{
    [Fact]
    public async Task RequestFreezesDraftAndCreatesPendingDeploymentReview()
    {
        var fixture = new Fixture(OrchestrationVersionStatus.Draft);

        var reviewId = await fixture.Request.Handle(
            new RequestOrchestrationVersionReviewCommand(
                fixture.Version.Id.ToString(),
                OrchestrationVersionReviewOperation.Deploy,
                "designer"),
            CancellationToken.None);

        var review = Assert.Single(fixture.CreatedReviews);
        Assert.Equal(review.Id.ToString(), reviewId);
        Assert.Equal(OrchestrationVersionReviewApprovalStatus.Pending, review.ApprovalStatus);
        Assert.Equal(OrchestrationVersionReviewApplicationStatus.Pending, review.ApplicationStatus);
        Assert.Equal(OrchestrationVersionStatus.Draft, review.FromStatus);
        Assert.Equal(OrchestrationVersionStatus.PendingDeployment, review.PendingStatus);
        Assert.Equal(OrchestrationVersionStatus.Deployed, review.TargetStatus);
        Assert.Equal(OrchestrationVersionStatus.PendingDeployment, fixture.Version.Status);
        Assert.Equal($"open:{fixture.Version.OrchestrationDefinitionId}", fixture.Version.DraftSlot);
    }

    [Fact]
    public async Task RequestRejectsLifecycleOperationThatDoesNotMatchTheCurrentVersionState()
    {
        var fixture = new Fixture(OrchestrationVersionStatus.Deployed);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Request.Handle(
            new RequestOrchestrationVersionReviewCommand(
                fixture.Version.Id.ToString(),
                OrchestrationVersionReviewOperation.Archive,
                "designer"),
            CancellationToken.None));

        Assert.Contains("Cannot request", exception.Message, StringComparison.Ordinal);
        Assert.Empty(fixture.CreatedReviews);
        await fixture.VersionRepository.DidNotReceive().Update(Arg.Any<OrchestrationVersion>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ApproveMarksReviewEligibleWithoutChangingTheFrozenVersion()
    {
        var fixture = new Fixture(OrchestrationVersionStatus.PendingDeployment);
        var review = fixture.CreateReview();
        fixture.ReviewRepository.GetById(review.Id, Arg.Any<CancellationToken>()).Returns(review);

        var result = await fixture.Approve.Handle(
            new ApproveOrchestrationVersionReviewCommand(review.Id.ToString(), "approver"),
            CancellationToken.None);

        Assert.True(result);
        Assert.Equal(OrchestrationVersionReviewApprovalStatus.Approved, review.ApprovalStatus);
        Assert.Equal("approver", review.DecidedBy);
        await fixture.VersionRepository.DidNotReceive().Update(Arg.Any<OrchestrationVersion>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RejectRestoresVersionAndRecordsOptionalComment()
    {
        var fixture = new Fixture(OrchestrationVersionStatus.PendingDeprecation);
        var review = fixture.CreateReview(
            OrchestrationVersionReviewOperation.Deprecate,
            OrchestrationVersionStatus.Deployed,
            OrchestrationVersionStatus.PendingDeprecation,
            OrchestrationVersionStatus.Deprecated);
        fixture.ReviewRepository.GetById(review.Id, Arg.Any<CancellationToken>()).Returns(review);

        var result = await fixture.Reject.Handle(
            new RejectOrchestrationVersionReviewCommand(review.Id.ToString(), "reviewer", "Needs a rollout plan."),
            CancellationToken.None);

        Assert.True(result);
        Assert.Equal(OrchestrationVersionReviewApprovalStatus.Rejected, review.ApprovalStatus);
        Assert.Equal(OrchestrationVersionReviewApplicationStatus.Aborted, review.ApplicationStatus);
        Assert.Equal("Needs a rollout plan.", review.RejectionComment);
        Assert.Equal(OrchestrationVersionStatus.Deployed, fixture.Version.Status);
        Assert.Equal("reviewer", fixture.Version.UpdatedBy);
    }

    [Fact]
    public async Task AbortApprovedReviewRestoresVersionWithoutChangingTheApprovalDecision()
    {
        var fixture = new Fixture(OrchestrationVersionStatus.PendingArchive);
        var review = fixture.CreateReview(
            OrchestrationVersionReviewOperation.Archive,
            OrchestrationVersionStatus.Deprecated,
            OrchestrationVersionStatus.PendingArchive,
            OrchestrationVersionStatus.Archived);
        review.ApprovalStatus = OrchestrationVersionReviewApprovalStatus.Approved;
        fixture.ReviewRepository.GetById(review.Id, Arg.Any<CancellationToken>()).Returns(review);

        var result = await fixture.Abort.Handle(
            new AbortOrchestrationVersionReviewCommand(review.Id.ToString(), "designer", "Release window closed."),
            CancellationToken.None);

        Assert.True(result);
        Assert.Equal(OrchestrationVersionReviewApprovalStatus.Approved, review.ApprovalStatus);
        Assert.Equal(OrchestrationVersionReviewApplicationStatus.Aborted, review.ApplicationStatus);
        Assert.Equal("Release window closed.", review.AbortReason);
        Assert.Equal(OrchestrationVersionStatus.Deprecated, fixture.Version.Status);
    }

    [Fact]
    public async Task ApplyRejectsAReviewThatHasNotBeenApproved()
    {
        var fixture = new Fixture(OrchestrationVersionStatus.PendingDeployment);
        var review = fixture.CreateReview();
        fixture.ReviewRepository.GetById(review.Id, Arg.Any<CancellationToken>()).Returns(review);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Apply.Handle(
            new ApplyOrchestrationVersionReviewCommand(review.Id.ToString(), "deployer", "1.0.0", string.Empty, string.Empty, string.Empty),
            CancellationToken.None));

        Assert.Contains("approved", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class Fixture
    {
        public Fixture(OrchestrationVersionStatus status)
        {
            Version = new OrchestrationVersion
            {
                Id = Id.New(),
                OrchestrationDefinitionId = Id.New(),
                Version = status == OrchestrationVersionStatus.Draft || status == OrchestrationVersionStatus.PendingDeployment
                    ? null
                    : new SemanticVersion(1, 0, 0),
                Status = status,
                Checksum = new Checksum("draft:test"),
                CreatedBy = "designer",
            };
            Version.DraftSlot = $"open:{Version.OrchestrationDefinitionId}";

            VersionRepository.GetById(Version.Id, Arg.Any<CancellationToken>()).Returns(Version);
            VersionRepository.Update(Arg.Do<OrchestrationVersion>(updated =>
            {
                Version.Status = updated.Status;
                Version.UpdatedBy = updated.UpdatedBy;
                Version.UpdatedOnUtc = updated.UpdatedOnUtc;
                Version.DraftSlot = updated.DraftSlot;
            }), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
            ReviewRepository.Create(Arg.Do<OrchestrationVersionReview>(CreatedReviews.Add), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
            ReviewRepository.Update(Arg.Any<OrchestrationVersionReview>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
            UnitOfWork.ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>())
                .Returns(call => call.ArgAt<Func<CancellationToken, Task>>(0)(call.ArgAt<CancellationToken>(1)));

            Request = new RequestOrchestrationVersionReviewCommandHandler(VersionRepository, ReviewRepository, Policy, UnitOfWork);
            Approve = new ApproveOrchestrationVersionReviewCommandHandler(ReviewRepository);
            Reject = new RejectOrchestrationVersionReviewCommandHandler(ReviewRepository, VersionRepository, UnitOfWork);
            Abort = new AbortOrchestrationVersionReviewCommandHandler(ReviewRepository, VersionRepository, UnitOfWork);
            Apply = new ApplyOrchestrationVersionReviewCommandHandler(
                ReviewRepository,
                VersionRepository,
                Substitute.For<IOrchestrationDefinitionRepository>(),
                Policy,
                Substitute.For<IOrchestrationVersionArtifactSnapshotBuilder>(),
                Substitute.For<IOrchestrationArtifactPayloadFactory>(),
                Substitute.For<IOrchestrationArtifactDslValidationService>(),
                Substitute.For<IArtifactPublicationApplicationService>(),
                Substitute.For<IArtifactRepository>(),
                UnitOfWork);
        }

        public OrchestrationVersion Version { get; }
        public IOrchestrationVersionRepository VersionRepository { get; } = Substitute.For<IOrchestrationVersionRepository>();
        public IOrchestrationVersionReviewRepository ReviewRepository { get; } = Substitute.For<IOrchestrationVersionReviewRepository>();
        public IControlPlaneUnitOfWork UnitOfWork { get; } = Substitute.For<IControlPlaneUnitOfWork>();
        public IOrchestrationVersionTransitionPolicy Policy { get; } = new OrchestrationVersionTransitionPolicy();
        public List<OrchestrationVersionReview> CreatedReviews { get; } = new();
        public RequestOrchestrationVersionReviewCommandHandler Request { get; }
        public ApproveOrchestrationVersionReviewCommandHandler Approve { get; }
        public RejectOrchestrationVersionReviewCommandHandler Reject { get; }
        public AbortOrchestrationVersionReviewCommandHandler Abort { get; }
        public ApplyOrchestrationVersionReviewCommandHandler Apply { get; }

        public OrchestrationVersionReview CreateReview(
            OrchestrationVersionReviewOperation operation = OrchestrationVersionReviewOperation.Deploy,
            OrchestrationVersionStatus from = OrchestrationVersionStatus.Draft,
            OrchestrationVersionStatus pending = OrchestrationVersionStatus.PendingDeployment,
            OrchestrationVersionStatus target = OrchestrationVersionStatus.Deployed)
            => new()
            {
                Id = Id.New(),
                OrchestrationVersionId = Version.Id,
                Operation = operation,
                FromStatus = from,
                PendingStatus = pending,
                TargetStatus = target,
                ExpectedChecksum = Version.Checksum.Value,
                RequestedBy = "designer",
            };
    }
}
