using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;
using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Handles get orchestration definitions query requests.
/// </summary>
public sealed class GetOrchestrationDefinitionsQueryHandler : IRequestHandler<GetOrchestrationDefinitionsQuery, ApplicationPagedResult<OrchestrationDefinitionModel>>
{
    private readonly IOrchestrationDefinitionRepository _repository;
    private readonly IOrchestrationDefinitionApplicationMapper _mapper;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetOrchestrationDefinitionsQueryHandler"/> class.
    /// </summary>
    /// <param name="repository">Repository dependency.</param>
    /// <param name="mapper">Mapper dependency.</param>
    public GetOrchestrationDefinitionsQueryHandler(IOrchestrationDefinitionRepository repository, IOrchestrationDefinitionApplicationMapper mapper)
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
    public async Task<ApplicationPagedResult<OrchestrationDefinitionModel>> Handle(GetOrchestrationDefinitionsQuery request, CancellationToken cancellationToken)
    {
        PagedResult<global::Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.OrchestrationDefinition> rows = await _repository.GetAll(
            PrimitiveParser.ToStoragePagedSettings(request.Settings),
            cancellationToken);

        return _mapper.ToPagedModel(rows);
    }
}


