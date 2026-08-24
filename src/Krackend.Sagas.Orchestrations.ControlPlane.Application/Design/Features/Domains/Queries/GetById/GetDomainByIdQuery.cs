using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Retrieves one domain by identifier.
/// </summary>
public sealed record GetDomainByIdQuery(string DomainId) : IRequest<DomainModel>;
