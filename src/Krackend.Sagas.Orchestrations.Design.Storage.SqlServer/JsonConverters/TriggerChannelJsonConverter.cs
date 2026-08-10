using Krackend.Sagas.Orchestrations.Design.Storage.SqlServer.JsonModels;

namespace Krackend.Sagas.Orchestrations.Design.Storage.SqlServer.JsonConverters;

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
