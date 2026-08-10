using Krackend.Sagas.Orchestrations.Design.Storage.SqlServer.JsonModels;

namespace Krackend.Sagas.Orchestrations.Design.Storage.SqlServer.JsonConverters;

/// <summary>
/// Represents ConditionConfigurationJsonConverter.
/// </summary>
internal sealed class ConditionConfigurationJsonConverter : PolymorphicJsonConverter<ConditionConfigurationJsonModel>
{
    protected override string DiscriminatorPropertyName => "$type";

    protected override IReadOnlyDictionary<string, Type> TypeByDiscriminator => new Dictionary<string, Type>
    {
        ["dsl"] = typeof(DslConditionConfigurationJsonModel),
    };
}
