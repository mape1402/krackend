using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;
using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Handles set task compensation transformation command requests.
/// </summary>
public sealed class SetTaskCompensationTransformationCommandHandler : IRequestHandler<SetTaskCompensationTransformationCommand, bool>
{
    private readonly ITaskRepository _repository;

    /// <summary>
    /// Initializes a new instance of the <see cref="SetTaskCompensationTransformationCommandHandler"/> class.
    /// </summary>
    /// <param name="repository">Repository dependency.</param>
    public SetTaskCompensationTransformationCommandHandler(ITaskRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    /// <inheritdoc />
    public async Task<bool> Handle(SetTaskCompensationTransformationCommand request, CancellationToken cancellationToken)
    {
        await _repository.SetCompensationTransformation(
            PrimitiveParser.ParseId(request.Id),
            request.Transformation,
            cancellationToken);
        return true;
    }
}

