using Krackend.Sagas.Orchestrations.Design.Storage;
using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.Design.Interaction;

/// <summary>
/// Handles domain by id query.
/// </summary>
public sealed class GetDomainByIdQueryHandler : IRequestHandler<GetDomainByIdQuery, DomainModel>
{
    private readonly IDomainRepository _repository;
    private readonly IDomainInteractionMapper _mapper;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetDomainByIdQueryHandler"/> class.
    /// </summary>
    /// <param name="repository">Domain repository dependency.</param>
    /// <param name="mapper">Domain mapper dependency.</param>
    public GetDomainByIdQueryHandler(IDomainRepository repository, IDomainInteractionMapper mapper)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
    }

    /// <inheritdoc />
    public async Task<DomainModel> Handle(GetDomainByIdQuery request, CancellationToken cancellationToken)
    {
        var model = await _repository.GetById(PrimitiveParser.ParseId(request.DomainId), cancellationToken);
        return _mapper.ToModel(model);
    }
}
