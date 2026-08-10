using Krackend.Sagas.Orchestrations.Design.Storage.SqlServer.JsonModels;

namespace Krackend.Sagas.Orchestrations.Design.Storage.SqlServer.JsonConverters;

/// <summary>
/// Represents TransformationConfigurationJsonConverter.
/// </summary>
internal sealed class TransformationConfigurationJsonConverter : PolymorphicJsonConverter<TransformationConfigurationJsonModel>
{
    protected override string DiscriminatorPropertyName => "$type";

    protected override IReadOnlyDictionary<string, Type> TypeByDiscriminator => new Dictionary<string, Type>
    {
        ["dsl"] = typeof(DslTransformationConfigurationJsonModel),
    };
}
