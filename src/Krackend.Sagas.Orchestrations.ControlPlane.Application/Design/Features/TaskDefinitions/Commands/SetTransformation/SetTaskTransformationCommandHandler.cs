using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;
using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Handles set task transformation command requests.
/// </summary>
public sealed class SetTaskTransformationCommandHandler : IRequestHandler<SetTaskTransformationCommand, bool>
{
    private readonly ITaskRepository _repository;

    /// <summary>
    /// Initializes a new instance of the <see cref="SetTaskTransformationCommandHandler"/> class.
    /// </summary>
    /// <param name="repository">Repository dependency.</param>
    public SetTaskTransformationCommandHandler(ITaskRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    /// <summary>
    /// Handles the request.
    /// </summary>
    /// <param name="request">Request to process.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>True when the operation completes successfully.</returns>
    public async Task<bool> Handle(SetTaskTransformationCommand request, CancellationToken cancellationToken)
    {
        await _repository.SetTransformation(PrimitiveParser.ParseId(request.Id), request.Transformation, cancellationToken);
        return true;
    }
}

