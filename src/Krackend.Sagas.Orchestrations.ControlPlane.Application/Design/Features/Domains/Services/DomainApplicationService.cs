using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Provides interaction operations for domain catalog entries.
/// </summary>
public sealed class DomainApplicationService : IDomainApplicationService
{
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="DomainApplicationService"/> class.
    /// </summary>
    /// <param name="mediator">Mediator dependency.</param>
    public DomainApplicationService(IMediator mediator)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
    }

    /// <inheritdoc />
    public Task<string> Upsert(UpsertDomainCommand command, CancellationToken cancellationToken = default)
    {
        return _mediator.Send(command, cancellationToken);
    }

    /// <inheritdoc />
    public Task<bool> SetIsActive(SetDomainIsActiveCommand command, CancellationToken cancellationToken = default)
    {
        return _mediator.Send(command, cancellationToken);
    }

    /// <inheritdoc />
    public Task<DomainModel> GetById(GetDomainByIdQuery query, CancellationToken cancellationToken = default)
    {
        return _mediator.Send(query, cancellationToken);
    }

    /// <inheritdoc />
    public Task<ApplicationPagedResult<DomainModel>> GetAll(GetDomainsQuery query, CancellationToken cancellationToken = default)
    {
        return _mediator.Send(query, cancellationToken);
    }
}
