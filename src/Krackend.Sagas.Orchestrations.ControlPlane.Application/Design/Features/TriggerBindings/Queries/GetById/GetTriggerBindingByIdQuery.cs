using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Represents get trigger binding by id query.
/// </summary>
public sealed record GetTriggerBindingByIdQuery(string Id) : IRequest<TriggerBindingModel>;

