using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;
using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Handles get orchestration version by id query requests.
/// </summary>
public sealed class GetOrchestrationVersionByIdQueryHandler : IRequestHandler<GetOrchestrationVersionByIdQuery, OrchestrationVersionModel>
{
    private readonly IOrchestrationVersionRepository _repository;
    private readonly IOrchestrationVersionApplicationMapper _mapper;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetOrchestrationVersionByIdQueryHandler"/> class.
    /// </summary>
    /// <param name="repository">Repository dependency.</param>
    /// <param name="mapper">Mapper dependency.</param>
    public GetOrchestrationVersionByIdQueryHandler(IOrchestrationVersionRepository repository, IOrchestrationVersionApplicationMapper mapper)
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
    public async Task<OrchestrationVersionModel> Handle(GetOrchestrationVersionByIdQuery request, CancellationToken cancellationToken)
    {
        global::Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.OrchestrationVersion entity = await _repository.GetById(PrimitiveParser.ParseId(request.Id), cancellationToken);
        return _mapper.ToModel(entity);
    }
}


