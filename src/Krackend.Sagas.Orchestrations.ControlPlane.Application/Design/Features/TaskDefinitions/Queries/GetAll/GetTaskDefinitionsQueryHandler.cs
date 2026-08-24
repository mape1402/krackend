using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;
using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Handles get task definitions query requests.
/// </summary>
public sealed class GetTaskDefinitionsQueryHandler : IRequestHandler<GetTaskDefinitionsQuery, IEnumerable<TaskDefinitionModel>>
{
    private readonly ITaskRepository _repository;
    private readonly ITaskDefinitionApplicationMapper _mapper;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetTaskDefinitionsQueryHandler"/> class.
    /// </summary>
    /// <param name="repository">Repository dependency.</param>
    /// <param name="mapper">Mapper dependency.</param>
    public GetTaskDefinitionsQueryHandler(ITaskRepository repository, ITaskDefinitionApplicationMapper mapper)
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
    public async Task<IEnumerable<TaskDefinitionModel>> Handle(GetTaskDefinitionsQuery request, CancellationToken cancellationToken)
    {
        IEnumerable<global::Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.TaskDefinition> rows = await _repository.GetAll(
            PrimitiveParser.ParseId(request.StageDefinitionId),
            cancellationToken);

        return rows.Select(_mapper.ToModel);
    }
}

