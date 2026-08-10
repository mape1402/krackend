using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.Design.Interaction;

/// <summary>
/// Sets the active flag on a domain entry.
/// </summary>
public sealed record SetDomainIsActiveCommand(string DomainId, bool IsActive) : IRequest<bool>;
