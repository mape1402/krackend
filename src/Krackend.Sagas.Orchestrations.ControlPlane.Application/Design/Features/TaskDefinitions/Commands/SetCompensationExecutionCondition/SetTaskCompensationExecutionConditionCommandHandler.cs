using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;
using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Handles set task compensation execution condition command requests.
/// </summary>
public sealed class SetTaskCompensationExecutionConditionCommandHandler : IRequestHandler<SetTaskCompensationExecutionConditionCommand, bool>
{
    private readonly ITaskRepository _repository;

    /// <summary>
    /// Initializes a new instance of the <see cref="SetTaskCompensationExecutionConditionCommandHandler"/> class.
    /// </summary>
    /// <param name="repository">Repository dependency.</param>
    public SetTaskCompensationExecutionConditionCommandHandler(ITaskRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    /// <inheritdoc />
    public async Task<bool> Handle(SetTaskCompensationExecutionConditionCommand request, CancellationToken cancellationToken)
    {
        await _repository.SetCompensationExecutionCondition(
            PrimitiveParser.ParseId(request.Id),
            request.ExecutionCondition,
            cancellationToken);
        return true;
    }
}

