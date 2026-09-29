using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Provides interaction operations for orchestration metadata descriptors.
/// </summary>
public sealed class MetadataDescriptorApplicationService : IMetadataDescriptorApplicationService
{
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="MetadataDescriptorApplicationService"/> class.
    /// </summary>
    /// <param name="mediator">Mediator dependency.</param>
    public MetadataDescriptorApplicationService(IMediator mediator)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
    }

    /// <inheritdoc />
    public Task<string> Upsert(UpsertMetadataDescriptorCommand command, CancellationToken cancellationToken = default)
    {
        return _mediator.Send(command, cancellationToken);
    }

    /// <inheritdoc />
    public Task<bool> Delete(DeleteMetadataDescriptorCommand command, CancellationToken cancellationToken = default)
    {
        return _mediator.Send(command, cancellationToken);
    }

    /// <inheritdoc />
    public Task<MetadataDescriptorModel> GetById(GetMetadataDescriptorByIdQuery query, CancellationToken cancellationToken = default)
    {
        return _mediator.Send(query, cancellationToken);
    }

    /// <inheritdoc />
    public Task<ApplicationPagedResult<MetadataDescriptorModel>> GetAll(GetMetadataDescriptorsQuery query, CancellationToken cancellationToken = default)
    {
        return _mediator.Send(query, cancellationToken);
    }
}
