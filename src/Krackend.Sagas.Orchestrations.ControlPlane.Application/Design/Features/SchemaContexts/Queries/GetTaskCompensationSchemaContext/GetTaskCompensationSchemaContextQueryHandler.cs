using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Handles task compensation schema context queries.
/// </summary>
public sealed class GetTaskCompensationSchemaContextQueryHandler : IRequestHandler<GetTaskCompensationSchemaContextQuery, OrchestrationSchemaContext>
{
    private readonly IOrchestrationSchemaContextApplicationService _service;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetTaskCompensationSchemaContextQueryHandler"/> class.
    /// </summary>
    /// <param name="service">Application service dependency.</param>
    public GetTaskCompensationSchemaContextQueryHandler(IOrchestrationSchemaContextApplicationService service)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
    }

    /// <inheritdoc />
    public Task<OrchestrationSchemaContext> Handle(GetTaskCompensationSchemaContextQuery request, CancellationToken cancellationToken)
        => _service.GetForTaskCompensation(request, cancellationToken);
}

