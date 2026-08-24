using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;
using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Handles get orchestration definition by id query requests.
/// </summary>
public sealed class GetOrchestrationDefinitionByIdQueryHandler : IRequestHandler<GetOrchestrationDefinitionByIdQuery, OrchestrationDefinitionModel>
{
    private readonly IOrchestrationDefinitionRepository _repository;
    private readonly IOrchestrationDefinitionApplicationMapper _mapper;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetOrchestrationDefinitionByIdQueryHandler"/> class.
    /// </summary>
    /// <param name="repository">Repository dependency.</param>
    /// <param name="mapper">Mapper dependency.</param>
    public GetOrchestrationDefinitionByIdQueryHandler(IOrchestrationDefinitionRepository repository, IOrchestrationDefinitionApplicationMapper mapper)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
    }

    /// <summary>
    /// Handles the request.
    /// </summary>
    /// <param name="request">Request to process.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>Asynchronous operation result.</returns>
    public async Task<OrchestrationDefinitionModel> Handle(GetOrchestrationDefinitionByIdQuery request, CancellationToken cancellationToken)
    {
        global::Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.OrchestrationDefinition entity = await _repository.GetById(PrimitiveParser.ParseId(request.Id), cancellationToken);
        return _mapper.ToModel(entity);
    }
}


