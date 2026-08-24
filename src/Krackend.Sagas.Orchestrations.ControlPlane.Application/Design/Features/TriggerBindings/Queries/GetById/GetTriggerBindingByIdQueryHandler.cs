using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;
using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Handles get trigger binding by id query requests.
/// </summary>
public sealed class GetTriggerBindingByIdQueryHandler : IRequestHandler<GetTriggerBindingByIdQuery, TriggerBindingModel>
{
    private readonly ITriggerBindingRepository _repository;
    private readonly ITriggerBindingApplicationMapper _mapper;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetTriggerBindingByIdQueryHandler"/> class.
    /// </summary>
    /// <param name="repository">Repository dependency.</param>
    /// <param name="mapper">Mapper dependency.</param>
    public GetTriggerBindingByIdQueryHandler(ITriggerBindingRepository repository, ITriggerBindingApplicationMapper mapper)
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
    public async Task<TriggerBindingModel> Handle(GetTriggerBindingByIdQuery request, CancellationToken cancellationToken)
    {
        global::Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.TriggerBinding row = await _repository.GetById(
            PrimitiveParser.ParseId(request.Id),
            cancellationToken);

        return _mapper.ToModel(row);
    }
}

