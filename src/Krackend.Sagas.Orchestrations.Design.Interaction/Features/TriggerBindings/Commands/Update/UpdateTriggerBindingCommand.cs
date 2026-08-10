using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Design.Core;
using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.Design.Interaction;

/// <summary>
/// Represents update trigger binding command.
/// </summary>
public sealed record UpdateTriggerBindingCommand(
    string Id,
    string Key,
    TriggerType TriggerType,
    ITriggerChannel TriggerChannel,
    bool IsEnabled,
    string Description) : IRequest<bool>;

