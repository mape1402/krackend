using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Design.Storage;
using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.Design.Interaction;

/// <summary>
/// Handles return orchestration version to draft command requests.
/// </summary>
public sealed class ReturnOrchestrationVersionToDraftCommandHandler : IRequestHandler<ReturnOrchestrationVersionToDraftCommand, bool>
{
    private readonly IOrchestrationVersionRepository _repository;
    private readonly IOrchestrationVersionTransitionPolicy _transitionPolicy;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReturnOrchestrationVersionToDraftCommandHandler"/> class.
    /// </summary>
    /// <param name="repository">Repository dependency.</param>
    /// <param name="transitionPolicy">Status transition policy dependency.</param>
    public ReturnOrchestrationVersionToDraftCommandHandler(IOrchestrationVersionRepository repository, IOrchestrationVersionTransitionPolicy transitionPolicy)
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
    public async Task<bool> Handle(ReturnOrchestrationVersionToDraftCommand request, CancellationToken cancellationToken)
    {
        global::Krackend.Sagas.Orchestrations.Design.Core.OrchestrationVersion current = await _repository.GetById(PrimitiveParser.ParseId(request.Id), cancellationToken);

        _transitionPolicy.EnsureCanTransition(current.Status, OrchestrationVersionStatus.Draft);

        await _repository.SetStatus(current.Id, OrchestrationVersionStatus.Draft, cancellationToken);
        return true;
    }
}


