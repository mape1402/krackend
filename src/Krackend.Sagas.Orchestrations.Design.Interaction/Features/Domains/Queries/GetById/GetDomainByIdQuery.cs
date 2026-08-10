using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.Design.Interaction;

/// <summary>
/// Retrieves one domain by identifier.
/// </summary>
public sealed record GetDomainByIdQuery(string DomainId) : IRequest<DomainModel>;
