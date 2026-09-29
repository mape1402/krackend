using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;
using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Handles metadata descriptor delete command requests.
/// </summary>
public sealed class DeleteMetadataDescriptorCommandHandler : IRequestHandler<DeleteMetadataDescriptorCommand, bool>
{
    private readonly IMetadataDescriptorRepository _repository;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteMetadataDescriptorCommandHandler"/> class.
    /// </summary>
    /// <param name="repository">Metadata descriptor repository dependency.</param>
    public DeleteMetadataDescriptorCommandHandler(IMetadataDescriptorRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    /// <inheritdoc />
    public async Task<bool> Handle(DeleteMetadataDescriptorCommand request, CancellationToken cancellationToken)
    {
        await _repository.Delete(PrimitiveParser.ParseId(request.MetadataDescriptorId), cancellationToken);
        return true;
    }
}
