using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Design.JsonModels;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Design.JsonConverters;

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
