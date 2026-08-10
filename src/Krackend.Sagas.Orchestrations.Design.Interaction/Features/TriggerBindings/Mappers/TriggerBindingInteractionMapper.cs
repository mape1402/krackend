using Krackend.Sagas.Orchestrations.Design.Core;

namespace Krackend.Sagas.Orchestrations.Design.Interaction;

/// <summary>
/// Maps domain definitions to interaction models.
/// </summary>
public sealed class TriggerBindingInteractionMapper : ITriggerBindingInteractionMapper
{
    /// <summary>
    /// Maps a domain definition to an interaction model.
    /// </summary>
    /// <param name="source">Source value to map.</param>
    /// <returns>Operation result.</returns>
    public TriggerBindingModel ToModel(TriggerBinding source)
    {
        return new TriggerBindingModel
        {
            Id = source.Id.ToString(),
            OrchestrationVersionId = source.OrchestrationVersionId.ToString(),
            Key = source.Key ?? string.Empty,
            TriggerType = source.TriggerType,
            TriggerChannel = source.TriggerChannel,
            IsEnabled = source.IsEnabled,
            Description = source.Description ?? string.Empty,
        };
    }
}

