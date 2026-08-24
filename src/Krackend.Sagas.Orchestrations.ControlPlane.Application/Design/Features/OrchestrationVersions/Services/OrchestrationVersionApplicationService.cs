using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Provides interaction operations for orchestration version.
/// </summary>
public sealed class OrchestrationVersionApplicationService : IOrchestrationVersionApplicationService
{
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrchestrationVersionApplicationService"/> class.
    /// </summary>
    /// <param name="mediator">Mediator dependency.</param>
    public OrchestrationVersionApplicationService(IMediator mediator)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
    }

    /// <summary>
    /// Creates a new resource.
    /// </summary>
    /// <param name="command">Command to execute.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>Identifier or textual result of the operation.</returns>
    public Task<string> Create(CreateOrchestrationVersionCommand command, CancellationToken cancellationToken = default)
    {
        return _mediator.Send(command, cancellationToken);
    }

    /// <summary>
    /// Updates an existing resource.
    /// </summary>
    /// <param name="command">Command to execute.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>True when the operation completes successfully.</returns>
    public Task<bool> Update(UpdateOrchestrationVersionCommand command, CancellationToken cancellationToken = default)
    {
        return _mediator.Send(command, cancellationToken);
    }

    /// <summary>
    /// Moves the resource to InReview status.
    /// </summary>
    /// <param name="command">Command to execute.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>True when the operation completes successfully.</returns>
    public Task<bool> SetInReview(SetOrchestrationVersionInReviewCommand command, CancellationToken cancellationToken = default)
    {
        return _mediator.Send(command, cancellationToken);
    }

    /// <summary>
    /// Moves the resource back to Draft status.
    /// </summary>
    /// <param name="command">Command to execute.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>True when the operation completes successfully.</returns>
    public Task<bool> ReturnToDraft(ReturnOrchestrationVersionToDraftCommand command, CancellationToken cancellationToken = default)
    {
        return _mediator.Send(command, cancellationToken);
    }

    /// <summary>
    /// Reopens the review process for the resource.
    /// </summary>
    /// <param name="command">Command to execute.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>True when the operation completes successfully.</returns>
    public Task<bool> ReopenReview(ReopenOrchestrationVersionReviewCommand command, CancellationToken cancellationToken = default)
    {
        return _mediator.Send(command, cancellationToken);
    }

    /// <summary>
    /// Approves the resource.
    /// </summary>
    /// <param name="command">Command to execute.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>True when the operation completes successfully.</returns>
    public Task<bool> Approve(ApproveOrchestrationVersionCommand command, CancellationToken cancellationToken = default)
    {
        return _mediator.Send(command, cancellationToken);
    }

    /// <summary>
    /// Deploys the resource.
    /// </summary>
    /// <param name="command">Command to execute.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>True when the operation completes successfully.</returns>
    public Task<bool> Deploy(DeployOrchestrationVersionCommand command, CancellationToken cancellationToken = default)
    {
        return _mediator.Send(command, cancellationToken);
    }

    /// <summary>
    /// Marks the resource as deprecated.
    /// </summary>
    /// <param name="command">Command to execute.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>True when the operation completes successfully.</returns>
    public Task<bool> Deprecate(DeprecateOrchestrationVersionCommand command, CancellationToken cancellationToken = default)
    {
        return _mediator.Send(command, cancellationToken);
    }

    /// <summary>
    /// Archives the resource.
    /// </summary>
    /// <param name="command">Command to execute.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>True when the operation completes successfully.</returns>
    public Task<bool> Archive(ArchiveOrchestrationVersionCommand command, CancellationToken cancellationToken = default)
    {
        return _mediator.Send(command, cancellationToken);
    }

    /// <summary>
    /// Gets one resource by identifier.
    /// </summary>
    /// <param name="query">Query to execute.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>Asynchronous operation result.</returns>
    public Task<OrchestrationVersionModel> GetById(GetOrchestrationVersionByIdQuery query, CancellationToken cancellationToken = default)
    {
        return _mediator.Send(query, cancellationToken);
    }

    /// <summary>
    /// Gets resources that match the query.
    /// </summary>
    /// <param name="query">Query to execute.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>Paged interaction result.</returns>
    public Task<ApplicationPagedResult<OrchestrationVersionModel>> GetAll(GetOrchestrationVersionsQuery query, CancellationToken cancellationToken = default)
    {
        return _mediator.Send(query, cancellationToken);
    }
}

