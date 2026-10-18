namespace Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;

/// <summary>
/// Persists independent orchestration version review requests.
/// </summary>
public interface IOrchestrationVersionReviewRepository
{
    /// <summary>Creates a review request.</summary>
    Task Create(OrchestrationVersionReview review, CancellationToken cancellationToken = default);

    /// <summary>Returns a review by identifier.</summary>
    Task<OrchestrationVersionReview> GetById(Id reviewId, CancellationToken cancellationToken = default);

    /// <summary>Returns the review pending application for a version, when one exists.</summary>
    Task<OrchestrationVersionReview> GetPendingForVersion(Id orchestrationVersionId, CancellationToken cancellationToken = default);

    /// <summary>Returns the immutable review history for one orchestration version.</summary>
    Task<IReadOnlyCollection<OrchestrationVersionReview>> GetAllForVersion(
        Id orchestrationVersionId,
        CancellationToken cancellationToken = default);

    /// <summary>Persists changes to a review request.</summary>
    Task Update(OrchestrationVersionReview review, CancellationToken cancellationToken = default);
}
