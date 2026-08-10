using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Design.Core;
using Krackend.Sagas.Orchestrations.Design.Storage;
using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.Design.Interaction;

/// <summary>
/// Handles create branch rule definition command requests.
/// </summary>
public sealed class CreateBranchRuleDefinitionCommandHandler : IRequestHandler<CreateBranchRuleDefinitionCommand, string>
{
    private readonly IBranchRuleRepository _repository;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateBranchRuleDefinitionCommandHandler"/> class.
    /// </summary>
    /// <param name="repository">Repository dependency.</param>
    public CreateBranchRuleDefinitionCommandHandler(IBranchRuleRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    /// <summary>
    /// Handles the request.
    /// </summary>
    /// <param name="request">Request to process.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>Identifier or textual result of the operation.</returns>
    public async Task<string> Handle(CreateBranchRuleDefinitionCommand request, CancellationToken cancellationToken)
    {
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
}

