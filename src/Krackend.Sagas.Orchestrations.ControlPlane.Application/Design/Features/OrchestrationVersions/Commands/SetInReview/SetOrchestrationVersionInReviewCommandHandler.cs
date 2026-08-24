using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;
using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Handles set orchestration version in review command requests.
/// </summary>
public sealed class SetOrchestrationVersionInReviewCommandHandler : IRequestHandler<SetOrchestrationVersionInReviewCommand, bool>
{
    private readonly IOrchestrationVersionRepository _repository;
    private readonly IOrchestrationVersionTransitionPolicy _transitionPolicy;

    /// <summary>
    /// Initializes a new instance of the <see cref="SetOrchestrationVersionInReviewCommandHandler"/> class.
    /// </summary>
    /// <param name="repository">Repository dependency.</param>
    /// <param name="transitionPolicy">Status transition policy dependency.</param>
    public SetOrchestrationVersionInReviewCommandHandler(IOrchestrationVersionRepository repository, IOrchestrationVersionTransitionPolicy transitionPolicy)
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
    public async Task<bool> Handle(SetOrchestrationVersionInReviewCommand request, CancellationToken cancellationToken)
    {
        global::Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.OrchestrationVersion current = await _repository.GetById(PrimitiveParser.ParseId(request.Id), cancellationToken);

        if (current.Status != OrchestrationVersionStatus.Draft)
        {
            throw new InvalidOperationException("Set in review command only supports draft versions.");
        }

        _transitionPolicy.EnsureCanTransition(current.Status, OrchestrationVersionStatus.InReview);

        await _repository.SetStatus(current.Id, OrchestrationVersionStatus.InReview, cancellationToken);
        return true;
    }
}


