using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;
using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Handles get variable definitions query requests.
/// </summary>
public sealed class GetVariableDefinitionsQueryHandler : IRequestHandler<GetVariableDefinitionsQuery, IEnumerable<VariableDefinitionModel>>
{
    private readonly IVariableDefinitionRepository _repository;
    private readonly IVariableDefinitionApplicationMapper _mapper;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetVariableDefinitionsQueryHandler"/> class.
    /// </summary>
    /// <param name="repository">Repository dependency.</param>
    /// <param name="mapper">Mapper dependency.</param>
    public GetVariableDefinitionsQueryHandler(IVariableDefinitionRepository repository, IVariableDefinitionApplicationMapper mapper)
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
    public async Task<IEnumerable<VariableDefinitionModel>> Handle(GetVariableDefinitionsQuery request, CancellationToken cancellationToken)
    {
        IEnumerable<global::Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.VariableDefinition> rows = await _repository.GetAll(
            PrimitiveParser.ParseId(request.OrchestrationVersionId),
            cancellationToken);

        return rows.Select(_mapper.ToModel);
    }
}

