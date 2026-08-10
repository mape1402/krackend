using Krackend.Sagas.Orchestrations.Design.Storage;
using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.Design.Interaction;

/// <summary>
/// Handles disable trigger binding command requests.
/// </summary>
public sealed class DisableTriggerBindingCommandHandler : IRequestHandler<DisableTriggerBindingCommand, bool>
{
    private readonly ITriggerBindingRepository _repository;

    /// <summary>
    /// Initializes a new instance of the <see cref="DisableTriggerBindingCommandHandler"/> class.
    /// </summary>
    /// <param name="repository">Repository dependency.</param>
    public DisableTriggerBindingCommandHandler(ITriggerBindingRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    /// <summary>
    /// Handles the request.
    /// </summary>
    /// <param name="request">Request to process.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>True when the operation completes successfully.</returns>
    public async Task<bool> Handle(DisableTriggerBindingCommand request, CancellationToken cancellationToken)
    {
        await _repository.SetIsEnabled(PrimitiveParser.ParseId(request.Id), false, cancellationToken);
        return true;
    }
}

