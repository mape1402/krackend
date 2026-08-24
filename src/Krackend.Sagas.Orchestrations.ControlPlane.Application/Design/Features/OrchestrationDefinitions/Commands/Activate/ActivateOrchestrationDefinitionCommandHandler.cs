using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;
using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Handles activate orchestration definition command requests.
/// </summary>
public sealed class ActivateOrchestrationDefinitionCommandHandler : IRequestHandler<ActivateOrchestrationDefinitionCommand, bool>
{
    private readonly IOrchestrationDefinitionRepository _repository;

    /// <summary>
    /// Initializes a new instance of the <see cref="ActivateOrchestrationDefinitionCommandHandler"/> class.
    /// </summary>
    /// <param name="repository">Repository dependency.</param>
    public ActivateOrchestrationDefinitionCommandHandler(IOrchestrationDefinitionRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    /// <summary>
    /// Handles the request.
    /// </summary>
    /// <param name="request">Request to process.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>True when the operation completes successfully.</returns>
    public async Task<bool> Handle(ActivateOrchestrationDefinitionCommand request, CancellationToken cancellationToken)
    {
        await _repository.SetIsActive(PrimitiveParser.ParseId(request.Id), true, cancellationToken);
        return true;
    }
}


