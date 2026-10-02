using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;
using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Handles set trigger compensation transformation command requests.
/// </summary>
public sealed class SetTriggerCompensationTransformationCommandHandler : IRequestHandler<SetTriggerCompensationTransformationCommand, bool>
{
    private readonly ITriggerBindingRepository _repository;

    /// <summary>
    /// Initializes a new instance of the <see cref="SetTriggerCompensationTransformationCommandHandler"/> class.
    /// </summary>
    /// <param name="repository">Repository dependency.</param>
    public SetTriggerCompensationTransformationCommandHandler(ITriggerBindingRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    /// <inheritdoc />
    public async Task<bool> Handle(SetTriggerCompensationTransformationCommand request, CancellationToken cancellationToken)
    {
        await _repository.SetCompensationTransformation(
            PrimitiveParser.ParseId(request.Id),
            request.Transformation,
            cancellationToken);
        return true;
    }
}

