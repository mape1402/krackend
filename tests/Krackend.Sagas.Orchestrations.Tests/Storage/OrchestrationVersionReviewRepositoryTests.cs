namespace Krackend.Sagas.Orchestrations.Tests.Storage;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Design.Repositories;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

public sealed class OrchestrationVersionReviewRepositoryTests
{
    [Fact]
    public async Task GetAllReturnsLifecycleReviewsInReverseChronologicalOrderAndPagesThem()
    {
        var options = new DbContextOptionsBuilder<ControlPlaneDbContext>()
            .UseInMemoryDatabase($"krackend-review-list-{Guid.NewGuid():N}")
            .Options;
        var older = CreateReview();
        older.RequestedOnUtc = DateTime.UtcNow.AddMinutes(-1);
        var newer = CreateReview();
        newer.RequestedOnUtc = DateTime.UtcNow;

        await using (var setup = new ControlPlaneDbContext(options))
        {
            var repository = new OrchestrationVersionReviewRepository(setup);
            await repository.Create(older);
            await repository.Create(newer);
        }

        await using var verification = new ControlPlaneDbContext(options);
        var rows = await new OrchestrationVersionReviewRepository(verification).GetAll(
            new Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage.PagedSettings(1, 1, [], []));

        Assert.Equal(1, rows.PageNumber);
        Assert.Equal(2, rows.TotalPages);
        Assert.Equal(2, rows.TotalRows);
        Assert.Equal(newer.Id, Assert.Single(rows.Rows).Id);
    }

    [Fact]
    public async Task UpdateRejectsAStaleLifecycleDecisionFromAnotherControlPlaneInstance()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"krackend-review-concurrency-{Guid.NewGuid():N}.db");
        try
        {
            var options = new DbContextOptionsBuilder<ControlPlaneDbContext>()
                .UseSqlite($"Data Source={databasePath}")
                .Options;
            var review = CreateReview();

            await using (var setup = new ControlPlaneDbContext(options))
            {
                await setup.Database.EnsureCreatedAsync();
                await new OrchestrationVersionReviewRepository(setup).Create(review);
            }

            await using var firstContext = new ControlPlaneDbContext(options);
            await using var secondContext = new ControlPlaneDbContext(options);
            var firstRepository = new OrchestrationVersionReviewRepository(firstContext);
            var secondRepository = new OrchestrationVersionReviewRepository(secondContext);
            var first = await firstRepository.GetById(review.Id);
            var stale = await secondRepository.GetById(review.Id);

            first.ApprovalStatus = OrchestrationVersionReviewApprovalStatus.Approved;
            first.DecidedBy = "first-approver";
            first.DecidedOnUtc = DateTime.UtcNow;
            await firstRepository.Update(first);

            stale.ApprovalStatus = OrchestrationVersionReviewApprovalStatus.Approved;
            stale.DecidedBy = "second-approver";
            stale.DecidedOnUtc = DateTime.UtcNow;
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => secondRepository.Update(stale));

            Assert.Contains("another actor", exception.Message, StringComparison.OrdinalIgnoreCase);
            await using var verificationContext = new ControlPlaneDbContext(options);
            var persisted = await new OrchestrationVersionReviewRepository(verificationContext).GetById(review.Id);
            Assert.Equal("first-approver", persisted.DecidedBy);
            Assert.Equal(OrchestrationVersionReviewApprovalStatus.Approved, persisted.ApprovalStatus);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(databasePath))
            {
                File.Delete(databasePath);
            }
        }
    }

    private static OrchestrationVersionReview CreateReview()
        => new()
        {
            Id = Id.New(),
            OrchestrationVersionId = Id.New(),
            Operation = OrchestrationVersionReviewOperation.Deploy,
            FromStatus = OrchestrationVersionStatus.Draft,
            PendingStatus = OrchestrationVersionStatus.PendingDeployment,
            TargetStatus = OrchestrationVersionStatus.Deployed,
            ExpectedChecksum = "draft:test",
            RequestedBy = "designer",
            RequestedOnUtc = DateTime.UtcNow,
        };
}
