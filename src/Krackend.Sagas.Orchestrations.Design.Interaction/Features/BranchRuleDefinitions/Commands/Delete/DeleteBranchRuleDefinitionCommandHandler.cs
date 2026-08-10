using Krackend.Sagas.Orchestrations.Design.Storage;
using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.Design.Interaction;

/// <summary>
/// Handles delete branch rule definition command requests.
/// </summary>
public sealed class DeleteBranchRuleDefinitionCommandHandler : IRequestHandler<DeleteBranchRuleDefinitionCommand, bool>
{
    private readonly IBranchRuleRepository _repository;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteBranchRuleDefinitionCommandHandler"/> class.
    /// </summary>
    /// <param name="repository">Repository dependency.</param>
    public DeleteBranchRuleDefinitionCommandHandler(IBranchRuleRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    /// <summary>
    /// Handles the request.
    /// </summary>
    /// <param name="request">Request to process.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>True when the operation completes successfully.</returns>
    public async Task<bool> Handle(DeleteBranchRuleDefinitionCommand request, CancellationToken cancellationToken)
    {
        await _repository.Delete(PrimitiveParser.ParseId(request.Id), cancellationToken);
        return true;
    }
}

