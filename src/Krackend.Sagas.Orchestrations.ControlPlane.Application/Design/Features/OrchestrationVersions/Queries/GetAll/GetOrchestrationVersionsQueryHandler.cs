using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;
using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Handles get orchestration versions query requests.
/// </summary>
public sealed class GetOrchestrationVersionsQueryHandler : IRequestHandler<GetOrchestrationVersionsQuery, ApplicationPagedResult<OrchestrationVersionModel>>
{
    private readonly IOrchestrationVersionRepository _repository;
    private readonly IOrchestrationVersionApplicationMapper _mapper;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetOrchestrationVersionsQueryHandler"/> class.
    /// </summary>
    /// <param name="repository">Repository dependency.</param>
    /// <param name="mapper">Mapper dependency.</param>
    public GetOrchestrationVersionsQueryHandler(IOrchestrationVersionRepository repository, IOrchestrationVersionApplicationMapper mapper)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
    }

    /// <summary>
    /// Handles the request.
    /// </summary>
    /// <param name="request">Request to process.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>Paged interaction result.</returns>
    public async Task<ApplicationPagedResult<OrchestrationVersionModel>> Handle(GetOrchestrationVersionsQuery request, CancellationToken cancellationToken)
    {
        PagedResult<global::Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.OrchestrationVersion> rows = await _repository.GetAll(
            PrimitiveParser.ParseId(request.OrchestrationDefinitionId),
            PrimitiveParser.ToStoragePagedSettings(request.Settings),
            cancellationToken);

        return _mapper.ToPagedModel(rows);
    }
}


