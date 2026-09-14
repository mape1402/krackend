using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;
using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Handles create trigger binding command requests.
/// </summary>
public sealed class CreateTriggerBindingCommandHandler : IRequestHandler<CreateTriggerBindingCommand, string>
{
    private readonly ITriggerBindingRepository _repository;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateTriggerBindingCommandHandler"/> class.
    /// </summary>
    /// <param name="repository">Repository dependency.</param>
    public CreateTriggerBindingCommandHandler(ITriggerBindingRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    /// <summary>
    /// Handles the request.
    /// </summary>
    /// <param name="request">Request to process.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>Identifier or textual result of the operation.</returns>
    public async Task<string> Handle(CreateTriggerBindingCommand request, CancellationToken cancellationToken)
    {
        Id id = Id.New();

        TriggerBinding model = new()
        {
            Id = id,
            OrchestrationVersionId = PrimitiveParser.ParseId(request.OrchestrationVersionId),
            Key = request.Key,
            TriggerType = request.TriggerType,
            TriggerChannel = request.TriggerChannel,
            IsEnabled = request.IsEnabled,
            Description = request.Description,
        };

        await _repository.Create(model, cancellationToken);
        return id.ToString();
    }
}

