namespace Krackend.Sagas.Orchestrations.Abstractions.Artifacts;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Represents an immutable trigger-binding contract.
/// </summary>
public sealed record TriggerBindingArtifact(
    Id Id,
    TriggerType TriggerType,
    ITriggerChannelArtifact TriggerChannel,
    bool IsEnabled,
    string Description = "");
