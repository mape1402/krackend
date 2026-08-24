using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;
using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Handles get stage definitions query requests.
/// </summary>
public sealed class GetStageDefinitionsQueryHandler : IRequestHandler<GetStageDefinitionsQuery, IEnumerable<StageDefinitionModel>>
{
    private readonly IStageRepository _repository;
    private readonly IStageDefinitionApplicationMapper _mapper;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetStageDefinitionsQueryHandler"/> class.
    /// </summary>
    /// <param name="repository">Repository dependency.</param>
    /// <param name="mapper">Mapper dependency.</param>
    public GetStageDefinitionsQueryHandler(IStageRepository repository, IStageDefinitionApplicationMapper mapper)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
    }

    /// <summary>
    /// Handles the request.
    /// </summary>
    /// <param name="request">Request to process.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>Collection result.</returns>
    public async Task<IEnumerable<StageDefinitionModel>> Handle(GetStageDefinitionsQuery request, CancellationToken cancellationToken)
    {
        IEnumerable<global::Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.StageDefinition> rows = await _repository.GetAll(
            PrimitiveParser.ParseId(request.OrchestrationVersionId),
            cancellationToken);

        return rows.Select(_mapper.ToModel);
    }
}

