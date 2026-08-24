using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;
using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Handles enable trigger binding command requests.
/// </summary>
public sealed class EnableTriggerBindingCommandHandler : IRequestHandler<EnableTriggerBindingCommand, bool>
{
    private readonly ITriggerBindingRepository _repository;

    /// <summary>
    /// Initializes a new instance of the <see cref="EnableTriggerBindingCommandHandler"/> class.
    /// </summary>
    /// <param name="repository">Repository dependency.</param>
    public EnableTriggerBindingCommandHandler(ITriggerBindingRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    /// <summary>
    /// Handles the request.
    /// </summary>
    /// <param name="request">Request to process.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>True when the operation completes successfully.</returns>
    public async Task<bool> Handle(EnableTriggerBindingCommand request, CancellationToken cancellationToken)
    {
        await _repository.SetIsEnabled(PrimitiveParser.ParseId(request.Id), true, cancellationToken);
        return true;
    }
}

