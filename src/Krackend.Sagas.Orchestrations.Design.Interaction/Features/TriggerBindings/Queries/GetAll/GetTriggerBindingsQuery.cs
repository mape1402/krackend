using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.Design.Interaction;

/// <summary>
/// Represents get trigger bindings query.
/// </summary>
public sealed record GetTriggerBindingsQuery(string OrchestrationVersionId) : IRequest<IEnumerable<TriggerBindingModel>>;

