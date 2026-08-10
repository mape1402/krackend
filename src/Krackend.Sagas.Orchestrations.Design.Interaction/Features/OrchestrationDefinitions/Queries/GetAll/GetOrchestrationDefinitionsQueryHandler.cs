using Krackend.Sagas.Orchestrations.Design.Storage;
using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.Design.Interaction;

/// <summary>
/// Handles get orchestration definitions query requests.
/// </summary>
public sealed class GetOrchestrationDefinitionsQueryHandler : IRequestHandler<GetOrchestrationDefinitionsQuery, InteractionPagedResult<OrchestrationDefinitionModel>>
{
    private readonly IOrchestrationDefinitionRepository _repository;
    private readonly IOrchestrationDefinitionInteractionMapper _mapper;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetOrchestrationDefinitionsQueryHandler"/> class.
    /// </summary>
    /// <param name="repository">Repository dependency.</param>
    /// <param name="mapper">Mapper dependency.</param>
    public GetOrchestrationDefinitionsQueryHandler(IOrchestrationDefinitionRepository repository, IOrchestrationDefinitionInteractionMapper mapper)
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
    public async Task<InteractionPagedResult<OrchestrationDefinitionModel>> Handle(GetOrchestrationDefinitionsQuery request, CancellationToken cancellationToken)
    {
        PagedResult<global::Krackend.Sagas.Orchestrations.Design.Core.OrchestrationDefinition> rows = await _repository.GetAll(
            PrimitiveParser.ToStoragePagedSettings(request.Settings),
            cancellationToken);

        return _mapper.ToPagedModel(rows);
    }
}


