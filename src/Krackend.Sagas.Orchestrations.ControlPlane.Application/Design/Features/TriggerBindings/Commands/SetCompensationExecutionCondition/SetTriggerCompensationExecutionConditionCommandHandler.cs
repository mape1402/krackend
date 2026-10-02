using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;
using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Handles set trigger compensation execution condition command requests.
/// </summary>
public sealed class SetTriggerCompensationExecutionConditionCommandHandler : IRequestHandler<SetTriggerCompensationExecutionConditionCommand, bool>
{
    private readonly ITriggerBindingRepository _repository;

    /// <summary>
    /// Initializes a new instance of the <see cref="SetTriggerCompensationExecutionConditionCommandHandler"/> class.
    /// </summary>
    /// <param name="repository">Repository dependency.</param>
    public SetTriggerCompensationExecutionConditionCommandHandler(ITriggerBindingRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    /// <inheritdoc />
    public async Task<bool> Handle(SetTriggerCompensationExecutionConditionCommand request, CancellationToken cancellationToken)
    {
        await _repository.SetCompensationExecutionCondition(
            PrimitiveParser.ParseId(request.Id),
            request.ExecutionCondition,
            cancellationToken);
        return true;
    }
}

