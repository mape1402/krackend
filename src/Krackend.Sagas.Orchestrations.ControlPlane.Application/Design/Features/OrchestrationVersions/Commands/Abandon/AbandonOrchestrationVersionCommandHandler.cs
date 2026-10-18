namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;
using Pelican.Mediator;

/// <summary>Abandons a draft directly without generating a Review.</summary>
public sealed class AbandonOrchestrationVersionCommandHandler : IRequestHandler<AbandonOrchestrationVersionCommand, bool>
{
    private readonly IOrchestrationVersionRepository _repository;
    private readonly IOrchestrationVersionTransitionPolicy _transitionPolicy;

    /// <summary>Initializes a new instance.</summary>
    public AbandonOrchestrationVersionCommandHandler(
        IOrchestrationVersionRepository repository,
        IOrchestrationVersionTransitionPolicy transitionPolicy)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _transitionPolicy = transitionPolicy ?? throw new ArgumentNullException(nameof(transitionPolicy));
    }

    /// <inheritdoc />
    public async Task<bool> Handle(AbandonOrchestrationVersionCommand request, CancellationToken cancellationToken)
    {
        var version = await _repository.GetById(PrimitiveParser.ParseId(request.Id), cancellationToken);
        _transitionPolicy.EnsureCanTransition(version.Status, OrchestrationVersionStatus.Abandoned);
        version.Status = OrchestrationVersionStatus.Abandoned;
        version.UpdatedBy = request.AbandonedBy;
        version.UpdatedOnUtc = DateTime.UtcNow;
        await _repository.Update(version, cancellationToken);
        return true;
    }
}
