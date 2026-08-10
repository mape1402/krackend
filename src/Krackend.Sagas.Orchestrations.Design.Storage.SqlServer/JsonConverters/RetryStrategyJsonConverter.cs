using Krackend.Sagas.Orchestrations.Design.Storage.SqlServer.JsonModels;

namespace Krackend.Sagas.Orchestrations.Design.Storage.SqlServer.JsonConverters;

/// <summary>
/// Represents RetryStrategyJsonConverter.
/// </summary>
internal sealed class RetryStrategyJsonConverter : PolymorphicJsonConverter<RetryStrategyJsonModel>
{
    protected override string DiscriminatorPropertyName => "$type";

    protected override IReadOnlyDictionary<string, Type> TypeByDiscriminator => new Dictionary<string, Type>
    {
        ["fixed"] = typeof(FixedRetryStrategyJsonModel),
    };
}
