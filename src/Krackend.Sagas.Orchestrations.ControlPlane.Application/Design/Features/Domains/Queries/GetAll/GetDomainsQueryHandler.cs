using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;
using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Handles get domains query requests.
/// </summary>
public sealed class GetDomainsQueryHandler : IRequestHandler<GetDomainsQuery, ApplicationPagedResult<DomainModel>>
{
    private readonly IDomainRepository _repository;
    private readonly IDomainApplicationMapper _mapper;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetDomainsQueryHandler"/> class.
    /// </summary>
    /// <param name="repository">Domain repository dependency.</param>
    /// <param name="mapper">Domain mapper dependency.</param>
    public GetDomainsQueryHandler(IDomainRepository repository, IDomainApplicationMapper mapper)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
    }

    /// <inheritdoc />
    public async Task<ApplicationPagedResult<DomainModel>> Handle(GetDomainsQuery request, CancellationToken cancellationToken)
    {
        var paged = await _repository.GetAll(
            new PagedSettings(
                request.PagedSettings.PageNumber,
                request.PagedSettings.PageSize,
                request.PagedSettings.Filters?.Select(x => new QueryFilter(x.Field, x.Operator, x.Value)).ToArray()
                    ?? Array.Empty<QueryFilter>(),
                request.PagedSettings.Sorts?.Select(x => new QuerySort(x.Field, x.Descending)).ToArray()
                    ?? Array.Empty<QuerySort>()),
            request.SearchText,
            cancellationToken);

        return _mapper.ToPagedModel(paged);
    }
}
