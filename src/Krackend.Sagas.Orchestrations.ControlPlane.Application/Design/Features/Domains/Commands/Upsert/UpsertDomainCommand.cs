using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Creates or updates a domain entry.
/// </summary>
public sealed record UpsertDomainCommand(
    string Id,
    string Key,
    string DisplayName,
    string Description) : IRequest<string>;
