namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

using Pelican.Mediator;

/// <summary>Mediator-backed application service for independent lifecycle reviews.</summary>
public sealed class OrchestrationVersionReviewApplicationService : IOrchestrationVersionReviewApplicationService
{
    private readonly IMediator _mediator;

    public OrchestrationVersionReviewApplicationService(IMediator mediator)
        => _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));

    public Task<string> Request(RequestOrchestrationVersionReviewCommand command, CancellationToken cancellationToken = default)
        => _mediator.Send(command, cancellationToken);

    public Task<bool> Approve(ApproveOrchestrationVersionReviewCommand command, CancellationToken cancellationToken = default)
        => _mediator.Send(command, cancellationToken);

    public Task<bool> Reject(RejectOrchestrationVersionReviewCommand command, CancellationToken cancellationToken = default)
        => _mediator.Send(command, cancellationToken);

    public Task<bool> Abort(AbortOrchestrationVersionReviewCommand command, CancellationToken cancellationToken = default)
        => _mediator.Send(command, cancellationToken);

    public Task<bool> Apply(ApplyOrchestrationVersionReviewCommand command, CancellationToken cancellationToken = default)
        => _mediator.Send(command, cancellationToken);

    public Task<OrchestrationVersionReviewModel> GetById(GetOrchestrationVersionReviewByIdQuery query, CancellationToken cancellationToken = default)
        => _mediator.Send(query, cancellationToken);

    public Task<OrchestrationVersionReviewModel> GetPending(GetPendingOrchestrationVersionReviewQuery query, CancellationToken cancellationToken = default)
        => _mediator.Send(query, cancellationToken);

    public Task<IReadOnlyCollection<OrchestrationVersionReviewModel>> GetAll(
        GetOrchestrationVersionReviewsQuery query,
        CancellationToken cancellationToken = default)
        => _mediator.Send(query, cancellationToken);
}
