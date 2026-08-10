using Krackend.Sagas.Orchestrations.Design.Storage;
using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.Design.Interaction;

/// <summary>
/// Handles update branch rule definition command requests.
/// </summary>
public sealed class UpdateBranchRuleDefinitionCommandHandler : IRequestHandler<UpdateBranchRuleDefinitionCommand, bool>
{
    private readonly IBranchRuleRepository _repository;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateBranchRuleDefinitionCommandHandler"/> class.
    /// </summary>
    /// <param name="repository">Repository dependency.</param>
    public UpdateBranchRuleDefinitionCommandHandler(IBranchRuleRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    /// <summary>
    /// Handles the request.
    /// </summary>
    /// <param name="request">Request to process.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>True when the operation completes successfully.</returns>
    public async Task<bool> Handle(UpdateBranchRuleDefinitionCommand request, CancellationToken cancellationToken)
    {
        global::Krackend.Sagas.Orchestrations.Design.Core.BranchRuleDefinition current = await _repository.GetById(PrimitiveParser.ParseId(request.Id), cancellationToken);

        current.FromType = request.FromType;
        current.FromId = PrimitiveParser.ParseId(request.FromId);
        current.Condition = request.Condition;
        current.NavigateToType = request.NavigateToType;
        current.NavigateToId = PrimitiveParser.ParseId(request.NavigateToId);

        await _repository.Update(current, cancellationToken);
        return true;
    }
}

