using Krackend.Sagas.Orchestrations.Design.Storage;
using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.Design.Interaction;

/// <summary>
/// Handles get parallel group definitions query requests.
/// </summary>
public sealed class GetParallelGroupDefinitionsQueryHandler : IRequestHandler<GetParallelGroupDefinitionsQuery, IEnumerable<ParallelGroupDefinitionModel>>
{
    private readonly IParallelGroupRepository _repository;
    private readonly IParallelGroupDefinitionInteractionMapper _mapper;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetParallelGroupDefinitionsQueryHandler"/> class.
    /// </summary>
    /// <param name="repository">Repository dependency.</param>
    /// <param name="mapper">Mapper dependency.</param>
    public GetParallelGroupDefinitionsQueryHandler(IParallelGroupRepository repository, IParallelGroupDefinitionInteractionMapper mapper)
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
    public async Task<IEnumerable<ParallelGroupDefinitionModel>> Handle(GetParallelGroupDefinitionsQuery request, CancellationToken cancellationToken)
    {
        IEnumerable<global::Krackend.Sagas.Orchestrations.Design.Core.ParallelGroupDefinition> rows = await _repository.GetAll(
            PrimitiveParser.ParseId(request.StageDefinitionId),
            cancellationToken);

        return rows.Select(_mapper.ToModel);
    }
}

