using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;
using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Handles create branch rule definition command requests.
/// </summary>
public sealed class CreateBranchRuleDefinitionCommandHandler : IRequestHandler<CreateBranchRuleDefinitionCommand, string>
{
    private readonly IBranchRuleRepository _repository;
    private readonly IStageRepository _stageRepository;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateBranchRuleDefinitionCommandHandler"/> class.
    /// </summary>
    /// <param name="repository">Branch rule repository dependency.</param>
    /// <param name="stageRepository">Stage repository dependency.</param>
    public CreateBranchRuleDefinitionCommandHandler(
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
    /// <returns>Identifier or textual result of the operation.</returns>
    public async Task<string> Handle(CreateBranchRuleDefinitionCommand request, CancellationToken cancellationToken)
    {
        await EnsureForwardStageNavigation(request.FromId, request.NavigateToId, cancellationToken);

        Id id = Id.New();

        BranchRuleDefinition model = new()
        {
            Id = id,
            FromType = request.FromType,
            FromId = PrimitiveParser.ParseId(request.FromId),
            Condition = request.Condition,
            NavigateToType = request.NavigateToType,
            NavigateToId = PrimitiveParser.ParseId(request.NavigateToId),
        };

        await _repository.Create(model, cancellationToken);
        return id.ToString();
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

