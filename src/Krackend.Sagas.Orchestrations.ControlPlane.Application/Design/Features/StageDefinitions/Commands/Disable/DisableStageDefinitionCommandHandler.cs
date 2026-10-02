using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;
using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Handles disable stage definition command requests.
/// </summary>
public sealed class DisableStageDefinitionCommandHandler : IRequestHandler<DisableStageDefinitionCommand, bool>
{
    private readonly IStageRepository _repository;

    /// <summary>
    /// Initializes a new instance of the <see cref="DisableStageDefinitionCommandHandler"/> class.
    /// </summary>
    /// <param name="repository">Repository dependency.</param>
    public DisableStageDefinitionCommandHandler(IStageRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    /// <summary>
    /// Handles the request.
    /// </summary>
    /// <param name="request">Request to process.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>True when the operation completes successfully.</returns>
    public async Task<bool> Handle(DisableStageDefinitionCommand request, CancellationToken cancellationToken)
    {
        await _repository.SetIsEnabled(PrimitiveParser.ParseId(request.Id), false, cancellationToken);
        return true;
    }
}
