using Krackend.Sagas.Orchestrations.Contracts.Eventing;
using Krackend.Sagas.Orchestrations.Contracts.Events;
using Krackend.Sagas.Orchestrations.Design.Storage;
using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.Design.Interaction;

/// <summary>
/// Handles activate orchestration definition command requests.
/// </summary>
public sealed class ActivateOrchestrationDefinitionCommandHandler : IRequestHandler<ActivateOrchestrationDefinitionCommand, bool>
{
    private readonly IOrchestrationDefinitionRepository _repository;
    private readonly IIntegrationEventPublisher _eventPublisher;

    /// <summary>
    /// Initializes a new instance of the <see cref="ActivateOrchestrationDefinitionCommandHandler"/> class.
    /// </summary>
    /// <param name="repository">Repository dependency.</param>
    public ActivateOrchestrationDefinitionCommandHandler(
        IOrchestrationDefinitionRepository repository,
        IIntegrationEventPublisher eventPublisher)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _eventPublisher = eventPublisher ?? throw new ArgumentNullException(nameof(eventPublisher));
    }

    /// <summary>
    /// Handles the request.
    /// </summary>
    /// <param name="request">Request to process.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>True when the operation completes successfully.</returns>
    public async Task<bool> Handle(ActivateOrchestrationDefinitionCommand request, CancellationToken cancellationToken)
    {
        var current = await _repository.GetById(PrimitiveParser.ParseId(request.Id), cancellationToken);
        await _repository.SetIsActive(PrimitiveParser.ParseId(request.Id), true, cancellationToken);
        await _eventPublisher.Publish(new OrchestrationDefinitionUpdatedEvent(
            current.Id.ToString(),
            current.Key,
            current.Name,
            true,
            "web-ui",
            current.Id.ToString(),
            DateTime.UtcNow), cancellationToken);
        return true;
    }
}


