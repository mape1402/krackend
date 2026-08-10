using Krackend.Sagas.Orchestrations.Design.Storage;
using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.Design.Interaction;

/// <summary>
/// Handles get trigger bindings query requests.
/// </summary>
public sealed class GetTriggerBindingsQueryHandler : IRequestHandler<GetTriggerBindingsQuery, IEnumerable<TriggerBindingModel>>
{
    private readonly ITriggerBindingRepository _repository;
    private readonly ITriggerBindingInteractionMapper _mapper;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetTriggerBindingsQueryHandler"/> class.
    /// </summary>
    /// <param name="repository">Repository dependency.</param>
    /// <param name="mapper">Mapper dependency.</param>
    public GetTriggerBindingsQueryHandler(ITriggerBindingRepository repository, ITriggerBindingInteractionMapper mapper)
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
    public async Task<IEnumerable<TriggerBindingModel>> Handle(GetTriggerBindingsQuery request, CancellationToken cancellationToken)
    {
        IEnumerable<global::Krackend.Sagas.Orchestrations.Design.Core.TriggerBinding> rows = await _repository.GetAll(
            PrimitiveParser.ParseId(request.OrchestrationVersionId),
            cancellationToken);

        return rows.Select(_mapper.ToModel);
    }
}

