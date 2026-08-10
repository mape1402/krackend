using Krackend.Sagas.Orchestrations.Design.Storage.SqlServer.JsonModels;

namespace Krackend.Sagas.Orchestrations.Design.Storage.SqlServer.JsonConverters;

/// <summary>
/// Represents TaskConfigurationJsonConverter.
/// </summary>
internal sealed class TaskConfigurationJsonConverter : PolymorphicJsonConverter<TaskConfigurationJsonModel>
{
    protected override string DiscriminatorPropertyName => "$type";

    protected override IReadOnlyDictionary<string, Type> TypeByDiscriminator => new Dictionary<string, Type>
    {
        ["http"] = typeof(HttpTaskConfigurationJsonModel),
        ["messaging"] = typeof(MessagingTaskConfigurationJsonModel),
        ["plugin"] = typeof(PluginTaskConfigurationJsonModel),
        ["humanApproval"] = typeof(HumanApprovalTaskConfigurationJsonModel),
    };
}
