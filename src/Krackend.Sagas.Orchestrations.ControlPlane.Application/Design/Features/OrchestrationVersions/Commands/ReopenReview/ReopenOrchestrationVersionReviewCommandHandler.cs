using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;
using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Handles reopen orchestration version review command requests.
/// </summary>
public sealed class ReopenOrchestrationVersionReviewCommandHandler : IRequestHandler<ReopenOrchestrationVersionReviewCommand, bool>
{
    private readonly IOrchestrationVersionRepository _repository;
    private readonly IOrchestrationVersionTransitionPolicy _transitionPolicy;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReopenOrchestrationVersionReviewCommandHandler"/> class.
    /// </summary>
    /// <param name="repository">Repository dependency.</param>
    /// <param name="transitionPolicy">Status transition policy dependency.</param>
    public ReopenOrchestrationVersionReviewCommandHandler(IOrchestrationVersionRepository repository, IOrchestrationVersionTransitionPolicy transitionPolicy)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _transitionPolicy = transitionPolicy ?? throw new ArgumentNullException(nameof(transitionPolicy));
    }

    /// <summary>
    /// Handles the request.
    /// </summary>
    /// <param name="request">Request to process.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>True when the operation completes successfully.</returns>
    public async Task<bool> Handle(ReopenOrchestrationVersionReviewCommand request, CancellationToken cancellationToken)
    {
        global::Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.OrchestrationVersion current = await _repository.GetById(PrimitiveParser.ParseId(request.Id), cancellationToken);

        if (current.Status != OrchestrationVersionStatus.Approved)
        {
            throw new InvalidOperationException("Reopen review command only supports approved versions.");
        }

        _transitionPolicy.EnsureCanTransition(current.Status, OrchestrationVersionStatus.InReview);

        current.Status = OrchestrationVersionStatus.InReview;
        current.ApprovedOnUtc = default;
        current.ApprovedBy = string.Empty;
        current.UpdatedOnUtc = DateTime.UtcNow;
        current.UpdatedBy = request.UpdatedBy;

        await _repository.Update(current, cancellationToken);
        return true;
    }
}


