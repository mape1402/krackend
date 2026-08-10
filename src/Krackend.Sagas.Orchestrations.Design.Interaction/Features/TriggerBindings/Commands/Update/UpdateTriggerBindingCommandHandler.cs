using Krackend.Sagas.Orchestrations.Design.Storage;
using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.Design.Interaction;

/// <summary>
/// Handles update trigger binding command requests.
/// </summary>
public sealed class UpdateTriggerBindingCommandHandler : IRequestHandler<UpdateTriggerBindingCommand, bool>
{
    private readonly ITriggerBindingRepository _repository;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateTriggerBindingCommandHandler"/> class.
    /// </summary>
    /// <param name="repository">Repository dependency.</param>
    public UpdateTriggerBindingCommandHandler(ITriggerBindingRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    /// <summary>
    /// Handles the request.
    /// </summary>
    /// <param name="request">Request to process.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>True when the operation completes successfully.</returns>
    public async Task<bool> Handle(UpdateTriggerBindingCommand request, CancellationToken cancellationToken)
    {
        global::Krackend.Sagas.Orchestrations.Design.Core.TriggerBinding current = await _repository.GetById(PrimitiveParser.ParseId(request.Id), cancellationToken);

        current.Key = request.Key;
        current.TriggerType = request.TriggerType;
        current.TriggerChannel = request.TriggerChannel;
        current.IsEnabled = request.IsEnabled;
        current.Description = request.Description;

        await _repository.Update(current, cancellationToken);
        return true;
    }
}

