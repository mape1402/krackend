using Krackend.Sagas.Orchestrations.Design.Storage;
using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.Design.Interaction;

/// <summary>
/// Handles get domains query requests.
/// </summary>
public sealed class GetDomainsQueryHandler : IRequestHandler<GetDomainsQuery, InteractionPagedResult<DomainModel>>
{
    private readonly IDomainRepository _repository;
    private readonly IDomainInteractionMapper _mapper;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetDomainsQueryHandler"/> class.
    /// </summary>
    /// <param name="repository">Domain repository dependency.</param>
    /// <param name="mapper">Domain mapper dependency.</param>
    public GetDomainsQueryHandler(IDomainRepository repository, IDomainInteractionMapper mapper)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
    }

    /// <inheritdoc />
    public async Task<InteractionPagedResult<DomainModel>> Handle(GetDomainsQuery request, CancellationToken cancellationToken)
    {
        var paged = await _repository.GetAll(
            new Design.Storage.PagedSettings(
                request.PagedSettings.PageNumber,
                request.PagedSettings.PageSize,
                request.PagedSettings.Filters?.Select(x => new Design.Storage.QueryFilter(x.Field, x.Operator, x.Value)).ToArray()
                    ?? Array.Empty<Design.Storage.QueryFilter>(),
                request.PagedSettings.Sorts?.Select(x => new Design.Storage.QuerySort(x.Field, x.Descending)).ToArray()
                    ?? Array.Empty<Design.Storage.QuerySort>()),
            request.SearchText,
            cancellationToken);

        return _mapper.ToPagedModel(paged);
    }
}
