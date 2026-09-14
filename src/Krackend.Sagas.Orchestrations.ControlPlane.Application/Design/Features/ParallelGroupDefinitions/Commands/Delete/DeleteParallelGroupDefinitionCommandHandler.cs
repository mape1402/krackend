using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;
using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Handles delete parallel group definition command requests.
/// </summary>
public sealed class DeleteParallelGroupDefinitionCommandHandler : IRequestHandler<DeleteParallelGroupDefinitionCommand, bool>
{
    private readonly IParallelGroupRepository _repository;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteParallelGroupDefinitionCommandHandler"/> class.
    /// </summary>
    /// <param name="repository">Repository dependency.</param>
    public DeleteParallelGroupDefinitionCommandHandler(IParallelGroupRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    /// <summary>
    /// Handles the request.
    /// </summary>
    /// <param name="request">Request to process.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>True when the operation completes successfully.</returns>
    public async Task<bool> Handle(DeleteParallelGroupDefinitionCommand request, CancellationToken cancellationToken)
    {
        await _repository.Delete(PrimitiveParser.ParseId(request.Id), cancellationToken);
        return true;
    }
}

