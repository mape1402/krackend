using Krackend.Sagas.Orchestrations.Design.Storage;
using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.Design.Interaction;

/// <summary>
/// Handles set stage execution condition command requests.
/// </summary>
public sealed class SetStageExecutionConditionCommandHandler : IRequestHandler<SetStageExecutionConditionCommand, bool>
{
    private readonly IStageRepository _repository;

    /// <summary>
    /// Initializes a new instance of the <see cref="SetStageExecutionConditionCommandHandler"/> class.
    /// </summary>
    /// <param name="repository">Repository dependency.</param>
    public SetStageExecutionConditionCommandHandler(IStageRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    /// <summary>
    /// Handles the request.
    /// </summary>
    /// <param name="request">Request to process.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>True when the operation completes successfully.</returns>
    public async Task<bool> Handle(SetStageExecutionConditionCommand request, CancellationToken cancellationToken)
    {
        await _repository.SetExecutionCondition(PrimitiveParser.ParseId(request.Id), request.ExecutionCondition, cancellationToken);
        return true;
    }
}

