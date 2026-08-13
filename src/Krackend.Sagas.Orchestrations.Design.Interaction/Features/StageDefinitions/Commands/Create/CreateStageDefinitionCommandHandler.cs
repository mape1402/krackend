using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Design.Core;
using Krackend.Sagas.Orchestrations.Design.Storage;
using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.Design.Interaction;

/// <summary>
/// Handles create stage definition command requests.
/// </summary>
public sealed class CreateStageDefinitionCommandHandler : IRequestHandler<CreateStageDefinitionCommand, string>
{
    private readonly IStageRepository _repository;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateStageDefinitionCommandHandler"/> class.
    /// </summary>
    /// <param name="repository">Repository dependency.</param>
    public CreateStageDefinitionCommandHandler(IStageRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    /// <summary>
    /// Handles the request.
    /// </summary>
    /// <param name="request">Request to process.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>Identifier or textual result of the operation.</returns>
    public async Task<string> Handle(CreateStageDefinitionCommand request, CancellationToken cancellationToken)
    {
        Id id = Id.New();

        StageDefinition model = new()
        {
            Id = id,
            OrchestrationVersionId = PrimitiveParser.ParseId(request.OrchestrationVersionId),
            Key = request.Key,
            Name = request.Name,
            Description = request.Description,
            Order = request.Order,
            ExecutionCondition = request.ExecutionCondition,
            HasExecutionCondition = request.ExecutionCondition is not null,
        };

        await _repository.Create(model, cancellationToken);
        return id.ToString();
    }
}

