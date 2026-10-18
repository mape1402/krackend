namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Defines interaction operations for orchestration version.
/// </summary>
public interface IOrchestrationVersionApplicationService
{
    /// <summary>
    /// Creates a new resource.
    /// </summary>
    /// <param name="command">Command to execute.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>Identifier or textual result of the operation.</returns>
    Task<string> Create(CreateOrchestrationVersionCommand command, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing resource.
    /// </summary>
    /// <param name="command">Command to execute.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>True when the operation completes successfully.</returns>
    Task<bool> Update(UpdateOrchestrationVersionCommand command, CancellationToken cancellationToken = default);

    [Obsolete("Use IOrchestrationVersionReviewApplicationService to request and apply lifecycle transitions.")]
    Task<bool> SetInReview(SetOrchestrationVersionInReviewCommand command, CancellationToken cancellationToken = default);

    [Obsolete("Use IOrchestrationVersionReviewApplicationService to reject or abort a lifecycle review.")]
    Task<bool> ReturnToDraft(ReturnOrchestrationVersionToDraftCommand command, CancellationToken cancellationToken = default);

    [Obsolete("Use IOrchestrationVersionReviewApplicationService to request a new lifecycle review.")]
    Task<bool> ReopenReview(ReopenOrchestrationVersionReviewCommand command, CancellationToken cancellationToken = default);

    [Obsolete("Use IOrchestrationVersionReviewApplicationService to approve a lifecycle review.")]
    Task<bool> Approve(ApproveOrchestrationVersionCommand command, CancellationToken cancellationToken = default);

    [Obsolete("Use IOrchestrationVersionReviewApplicationService to apply a deployment review.")]
    Task<bool> Deploy(DeployOrchestrationVersionCommand command, CancellationToken cancellationToken = default);

    [Obsolete("Use IOrchestrationVersionReviewApplicationService to apply a deprecation review.")]
    Task<bool> Deprecate(DeprecateOrchestrationVersionCommand command, CancellationToken cancellationToken = default);

    [Obsolete("Use IOrchestrationVersionReviewApplicationService to apply an archive review.")]
    Task<bool> Archive(ArchiveOrchestrationVersionCommand command, CancellationToken cancellationToken = default);

    /// <summary>Abandons an editable draft directly without creating a review.</summary>
    /// <param name="command">Command to execute.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>True when the operation completes successfully.</returns>
    Task<bool> Abandon(AbandonOrchestrationVersionCommand command, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets one resource by identifier.
    /// </summary>
    /// <param name="query">Query to execute.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>Asynchronous operation result.</returns>
    Task<OrchestrationVersionModel> GetById(GetOrchestrationVersionByIdQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets resources that match the query.
    /// </summary>
    /// <param name="query">Query to execute.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>Paged interaction result.</returns>
    Task<ApplicationPagedResult<OrchestrationVersionModel>> GetAll(GetOrchestrationVersionsQuery query, CancellationToken cancellationToken = default);
}

