using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Design.Storage;
using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.Design.Interaction;

/// <summary>
/// Handles approve orchestration version command requests.
/// </summary>
public sealed class ApproveOrchestrationVersionCommandHandler : IRequestHandler<ApproveOrchestrationVersionCommand, bool>
{
    private readonly IOrchestrationVersionRepository _repository;
    private readonly IOrchestrationVersionTransitionPolicy _transitionPolicy;

    /// <summary>
    /// Initializes a new instance of the <see cref="ApproveOrchestrationVersionCommandHandler"/> class.
    /// </summary>
    /// <param name="repository">Repository dependency.</param>
    /// <param name="transitionPolicy">Status transition policy dependency.</param>
    public ApproveOrchestrationVersionCommandHandler(IOrchestrationVersionRepository repository, IOrchestrationVersionTransitionPolicy transitionPolicy)
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
    public async Task<bool> Handle(ApproveOrchestrationVersionCommand request, CancellationToken cancellationToken)
    {
        global::Krackend.Sagas.Orchestrations.Design.Core.OrchestrationVersion current = await _repository.GetById(PrimitiveParser.ParseId(request.Id), cancellationToken);

        _transitionPolicy.EnsureCanTransition(current.Status, OrchestrationVersionStatus.Approved);

        current.Status = OrchestrationVersionStatus.Approved;
        current.ApprovedOnUtc = DateTime.UtcNow;
        current.ApprovedBy = request.ApprovedBy;
        current.UpdatedOnUtc = DateTime.UtcNow;
        current.UpdatedBy = request.ApprovedBy;

        await _repository.Update(current, cancellationToken);
        return true;
    }
}


