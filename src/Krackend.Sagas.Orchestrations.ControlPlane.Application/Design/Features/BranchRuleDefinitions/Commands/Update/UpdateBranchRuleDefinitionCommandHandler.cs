using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Handles update branch rule definition command requests.
/// </summary>
public sealed class UpdateBranchRuleDefinitionCommandHandler : IRequestHandler<UpdateBranchRuleDefinitionCommand, bool>
{
    private readonly IBranchRuleRepository _repository;
    private readonly IStageRepository _stageRepository;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateBranchRuleDefinitionCommandHandler"/> class.
    /// </summary>
    /// <param name="repository">Branch rule repository dependency.</param>
    /// <param name="stageRepository">Stage repository dependency.</param>
    public UpdateBranchRuleDefinitionCommandHandler(
        IBranchRuleRepository repository,
        IStageRepository stageRepository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _stageRepository = stageRepository ?? throw new ArgumentNullException(nameof(stageRepository));
    }

    /// <summary>
    /// Handles the request.
    /// </summary>
    /// <param name="request">Request to process.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>True when the operation completes successfully.</returns>
    public async Task<bool> Handle(UpdateBranchRuleDefinitionCommand request, CancellationToken cancellationToken)
    {
        await EnsureForwardStageNavigation(request.FromId, request.NavigateToId, cancellationToken);

        global::Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.BranchRuleDefinition current = await _repository.GetById(PrimitiveParser.ParseId(request.Id), cancellationToken);

        current.FromType = request.FromType;
        current.FromId = PrimitiveParser.ParseId(request.FromId);
        current.Condition = request.Condition;
        current.NavigateToType = request.NavigateToType;
        current.NavigateToId = PrimitiveParser.ParseId(request.NavigateToId);

        await _repository.Update(current, cancellationToken);
        return true;
    }

    private async Task EnsureForwardStageNavigation(
        string sourceStageId,
        string targetStageId,
        CancellationToken cancellationToken)
    {
        var source = await _stageRepository.GetById(PrimitiveParser.ParseId(sourceStageId), cancellationToken);
        var target = await _stageRepository.GetById(PrimitiveParser.ParseId(targetStageId), cancellationToken);

        if (source.OrchestrationVersionId != target.OrchestrationVersionId)
        {
            throw new InvalidOperationException("Branch rules must navigate inside the same orchestration version.");
        }

        if (target.Order <= source.Order)
        {
            throw new InvalidOperationException("Branch rules must navigate to a later stage.");
        }
    }
}

