using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Design.Core;
using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.Design.Interaction;

/// <summary>
/// Represents create trigger binding command.
/// </summary>
public sealed record CreateTriggerBindingCommand(
    string OrchestrationVersionId,
    string Key,
    TriggerType TriggerType,
    ITriggerChannel TriggerChannel,
    bool IsEnabled,
    string Description) : IRequest<string>;

