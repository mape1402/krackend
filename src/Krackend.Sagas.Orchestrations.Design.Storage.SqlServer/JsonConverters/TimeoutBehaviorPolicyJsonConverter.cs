using Krackend.Sagas.Orchestrations.Design.Storage.SqlServer.JsonModels;

namespace Krackend.Sagas.Orchestrations.Design.Storage.SqlServer.JsonConverters;

/// <summary>
/// Represents TimeoutBehaviorPolicyJsonConverter.
/// </summary>
internal sealed class TimeoutBehaviorPolicyJsonConverter : PolymorphicJsonConverter<TimeoutBehaviorPolicyJsonModel>
{
    protected override string DiscriminatorPropertyName => "$type";

    protected override IReadOnlyDictionary<string, Type> TypeByDiscriminator => new Dictionary<string, Type>
    {
        ["fail"] = typeof(FailTimeoutBehaviorPolicyJsonModel),
        ["wait"] = typeof(WaitTimeoutBehaviorPolicyJsonModel),
        ["reconcile"] = typeof(ReconcileTimeoutBehaviorPolicyJsonModel),
    };
}
