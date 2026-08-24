using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;
using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Handles domain active state updates.
/// </summary>
public sealed class SetDomainIsActiveCommandHandler : IRequestHandler<SetDomainIsActiveCommand, bool>
{
    private readonly IDomainRepository _repository;

    /// <summary>
    /// Initializes a new instance of the <see cref="SetDomainIsActiveCommandHandler"/> class.
    /// </summary>
    /// <param name="repository">Domain repository dependency.</param>
    public SetDomainIsActiveCommandHandler(IDomainRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    /// <inheritdoc />
    public async Task<bool> Handle(SetDomainIsActiveCommand request, CancellationToken cancellationToken)
    {
        await _repository.SetIsActive(PrimitiveParser.ParseId(request.DomainId), request.IsActive, cancellationToken);
        return true;
    }
}
