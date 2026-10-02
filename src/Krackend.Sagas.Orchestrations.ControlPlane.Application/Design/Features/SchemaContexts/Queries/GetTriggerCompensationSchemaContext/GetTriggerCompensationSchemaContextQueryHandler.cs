using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Handles trigger compensation schema context queries.
/// </summary>
public sealed class GetTriggerCompensationSchemaContextQueryHandler : IRequestHandler<GetTriggerCompensationSchemaContextQuery, OrchestrationSchemaContext>
{
    private readonly IOrchestrationSchemaContextApplicationService _service;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetTriggerCompensationSchemaContextQueryHandler"/> class.
    /// </summary>
    /// <param name="service">Application service dependency.</param>
    public GetTriggerCompensationSchemaContextQueryHandler(IOrchestrationSchemaContextApplicationService service)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
    }

    /// <inheritdoc />
    public Task<OrchestrationSchemaContext> Handle(GetTriggerCompensationSchemaContextQuery request, CancellationToken cancellationToken)
        => _service.GetForTriggerCompensation(request, cancellationToken);
}

