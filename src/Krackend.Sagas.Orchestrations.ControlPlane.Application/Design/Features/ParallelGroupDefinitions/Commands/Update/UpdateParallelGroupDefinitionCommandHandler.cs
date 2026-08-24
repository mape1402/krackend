using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;
using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Handles update parallel group definition command requests.
/// </summary>
public sealed class UpdateParallelGroupDefinitionCommandHandler : IRequestHandler<UpdateParallelGroupDefinitionCommand, bool>
{
    private readonly IParallelGroupRepository _repository;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateParallelGroupDefinitionCommandHandler"/> class.
    /// </summary>
    /// <param name="repository">Repository dependency.</param>
    public UpdateParallelGroupDefinitionCommandHandler(IParallelGroupRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    /// <summary>
    /// Handles the request.
    /// </summary>
    /// <param name="request">Request to process.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>True when the operation completes successfully.</returns>
    public async Task<bool> Handle(UpdateParallelGroupDefinitionCommand request, CancellationToken cancellationToken)
    {
        global::Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.ParallelGroupDefinition current = await _repository.GetById(PrimitiveParser.ParseId(request.Id), cancellationToken);

        current.Name = request.Name ?? string.Empty;
        current.JoinPolicy = request.JoinPolicy;
        current.MaxParallelAgents = request.MaxParallelAgents;

        await _repository.Update(current, cancellationToken);
        return true;
    }
}

