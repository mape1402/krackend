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

    /// <summary>Returns lifecycle reviews across orchestration versions in reverse chronological order.</summary>
    Task<PagedResult<OrchestrationVersionReview>> GetAll(
        PagedSettings pagedSettings,
        CancellationToken cancellationToken = default);

    /// <summary>Searches and orders reviews in the storage provider before paging.</summary>
    Task<PagedResult<OrchestrationVersionReview>> Search(
        PagedSettings pagedSettings, string search, string sort, bool descending,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException("The configured review storage does not support catalog search.");

    /// <summary>Persists changes to a review request.</summary>
    Task Update(OrchestrationVersionReview review, CancellationToken cancellationToken = default);
}
