using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;
using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Handles get metadata descriptors query requests.
/// </summary>
public sealed class GetMetadataDescriptorsQueryHandler : IRequestHandler<GetMetadataDescriptorsQuery, ApplicationPagedResult<MetadataDescriptorModel>>
{
    private readonly IMetadataDescriptorRepository _repository;
    private readonly IMetadataDescriptorApplicationMapper _mapper;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetMetadataDescriptorsQueryHandler"/> class.
    /// </summary>
    /// <param name="repository">Metadata descriptor repository dependency.</param>
    /// <param name="mapper">Metadata descriptor mapper dependency.</param>
    public GetMetadataDescriptorsQueryHandler(
        IMetadataDescriptorRepository repository,
        IMetadataDescriptorApplicationMapper mapper)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
    }

    /// <inheritdoc />
    public async Task<ApplicationPagedResult<MetadataDescriptorModel>> Handle(GetMetadataDescriptorsQuery request, CancellationToken cancellationToken)
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
