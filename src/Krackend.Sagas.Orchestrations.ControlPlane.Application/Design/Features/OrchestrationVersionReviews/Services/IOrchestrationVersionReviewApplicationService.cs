namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>Defines the lifecycle-review boundary for orchestration versions.</summary>
public interface IOrchestrationVersionReviewApplicationService
{
    Task<string> Request(RequestOrchestrationVersionReviewCommand command, CancellationToken cancellationToken = default);
    Task<bool> Approve(ApproveOrchestrationVersionReviewCommand command, CancellationToken cancellationToken = default);
    Task<bool> Reject(RejectOrchestrationVersionReviewCommand command, CancellationToken cancellationToken = default);
    Task<bool> Abort(AbortOrchestrationVersionReviewCommand command, CancellationToken cancellationToken = default);
    Task<bool> Apply(ApplyOrchestrationVersionReviewCommand command, CancellationToken cancellationToken = default);
    Task<OrchestrationVersionReviewModel> GetById(GetOrchestrationVersionReviewByIdQuery query, CancellationToken cancellationToken = default);
    Task<OrchestrationVersionReviewModel> GetPending(GetPendingOrchestrationVersionReviewQuery query, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<OrchestrationVersionReviewModel>> GetAll(GetOrchestrationVersionReviewsQuery query, CancellationToken cancellationToken = default);
}
