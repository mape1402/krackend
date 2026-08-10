using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.Design.Interaction;

/// <summary>
/// Represents get trigger binding by id query.
/// </summary>
public sealed record GetTriggerBindingByIdQuery(string Id) : IRequest<TriggerBindingModel>;

