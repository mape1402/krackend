using Krackend.Sagas.Orchestrations.Design.Storage;
using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.Design.Interaction;

/// <summary>
/// Handles update stage definition command requests.
/// </summary>
public sealed class UpdateStageDefinitionCommandHandler : IRequestHandler<UpdateStageDefinitionCommand, bool>
{
    private readonly IStageRepository _repository;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateStageDefinitionCommandHandler"/> class.
    /// </summary>
    /// <param name="repository">Repository dependency.</param>
    public UpdateStageDefinitionCommandHandler(IStageRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    /// <summary>
    /// Handles the request.
    /// </summary>
    /// <param name="request">Request to process.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>True when the operation completes successfully.</returns>
    public async Task<bool> Handle(UpdateStageDefinitionCommand request, CancellationToken cancellationToken)
    {
        global::Krackend.Sagas.Orchestrations.Design.Core.StageDefinition current = await _repository.GetById(PrimitiveParser.ParseId(request.Id), cancellationToken);

        current.Key = request.Key;
        current.Name = request.Name;
        current.Description = request.Description;
        current.Order = request.Order;
        current.ExecutionCondition = request.ExecutionCondition;
        current.HasExecutionCondition = request.ExecutionCondition is not null;

        await _repository.Update(current, cancellationToken);
        return true;
    }
}

