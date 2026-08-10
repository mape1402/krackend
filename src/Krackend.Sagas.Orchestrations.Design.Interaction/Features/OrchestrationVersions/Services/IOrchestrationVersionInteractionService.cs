namespace Krackend.Sagas.Orchestrations.Design.Interaction;

/// <summary>
/// Defines interaction operations for orchestration version.
/// </summary>
public interface IOrchestrationVersionInteractionService
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

    /// <summary>
    /// Moves the resource to InReview status.
    /// </summary>
    /// <param name="command">Command to execute.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>True when the operation completes successfully.</returns>
    Task<bool> SetInReview(SetOrchestrationVersionInReviewCommand command, CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves the resource back to Draft status.
    /// </summary>
    /// <param name="command">Command to execute.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>True when the operation completes successfully.</returns>
    Task<bool> ReturnToDraft(ReturnOrchestrationVersionToDraftCommand command, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reopens the review process for the resource.
    /// </summary>
    /// <param name="command">Command to execute.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>True when the operation completes successfully.</returns>
    Task<bool> ReopenReview(ReopenOrchestrationVersionReviewCommand command, CancellationToken cancellationToken = default);

    /// <summary>
    /// Approves the resource.
    /// </summary>
    /// <param name="command">Command to execute.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>True when the operation completes successfully.</returns>
    Task<bool> Approve(ApproveOrchestrationVersionCommand command, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deploys the resource.
    /// </summary>
    /// <param name="command">Command to execute.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>True when the operation completes successfully.</returns>
    Task<bool> Deploy(DeployOrchestrationVersionCommand command, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks the resource as deprecated.
    /// </summary>
    /// <param name="command">Command to execute.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>True when the operation completes successfully.</returns>
    Task<bool> Deprecate(DeprecateOrchestrationVersionCommand command, CancellationToken cancellationToken = default);

    /// <summary>
    /// Archives the resource.
    /// </summary>
    /// <param name="command">Command to execute.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>True when the operation completes successfully.</returns>
    Task<bool> Archive(ArchiveOrchestrationVersionCommand command, CancellationToken cancellationToken = default);

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
    Task<InteractionPagedResult<OrchestrationVersionModel>> GetAll(GetOrchestrationVersionsQuery query, CancellationToken cancellationToken = default);
}

