using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;
using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Handles metadata descriptor by id query requests.
/// </summary>
public sealed class GetMetadataDescriptorByIdQueryHandler : IRequestHandler<GetMetadataDescriptorByIdQuery, MetadataDescriptorModel>
{
    private readonly IMetadataDescriptorRepository _repository;
    private readonly IMetadataDescriptorApplicationMapper _mapper;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetMetadataDescriptorByIdQueryHandler"/> class.
    /// </summary>
    /// <param name="repository">Metadata descriptor repository dependency.</param>
    /// <param name="mapper">Metadata descriptor mapper dependency.</param>
    public GetMetadataDescriptorByIdQueryHandler(
        IMetadataDescriptorRepository repository,
        IMetadataDescriptorApplicationMapper mapper)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
    }

    /// <inheritdoc />
    public async Task<MetadataDescriptorModel> Handle(GetMetadataDescriptorByIdQuery request, CancellationToken cancellationToken)
    {
        var descriptor = await _repository.GetById(PrimitiveParser.ParseId(request.MetadataDescriptorId), cancellationToken);
        return _mapper.ToModel(descriptor);
    }
}
