using Krackend.Sagas.Orchestrations.Design.Storage;
using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.Design.Interaction;

/// <summary>
/// Handles delete task definition command requests.
/// </summary>
public sealed class DeleteTaskDefinitionCommandHandler : IRequestHandler<DeleteTaskDefinitionCommand, bool>
{
    private readonly ITaskRepository _repository;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteTaskDefinitionCommandHandler"/> class.
    /// </summary>
    /// <param name="repository">Repository dependency.</param>
    public DeleteTaskDefinitionCommandHandler(ITaskRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    /// <summary>
    /// Handles the request.
    /// </summary>
    /// <param name="request">Request to process.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>True when the operation completes successfully.</returns>
    public async Task<bool> Handle(DeleteTaskDefinitionCommand request, CancellationToken cancellationToken)
    {
        await _repository.Delete(PrimitiveParser.ParseId(request.Id), cancellationToken);
        return true;
    }
}

