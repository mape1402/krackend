using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Design.JsonModels;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Design.JsonConverters;

/// <summary>
/// Represents TriggerChannelJsonConverter.
/// </summary>
internal sealed class TriggerChannelJsonConverter : PolymorphicJsonConverter<TriggerChannelJsonModel>
{
    protected override string DiscriminatorPropertyName => "$type";

    protected override IReadOnlyDictionary<string, Type> TypeByDiscriminator => new Dictionary<string, Type>
    {
        ["event"] = typeof(EventTriggerChannelJsonModel),
    };
}
