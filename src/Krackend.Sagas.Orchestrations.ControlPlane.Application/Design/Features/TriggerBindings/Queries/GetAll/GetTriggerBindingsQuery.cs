using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Represents get trigger bindings query.
/// </summary>
public sealed record GetTriggerBindingsQuery(string OrchestrationVersionId) : IRequest<IEnumerable<TriggerBindingModel>>;

