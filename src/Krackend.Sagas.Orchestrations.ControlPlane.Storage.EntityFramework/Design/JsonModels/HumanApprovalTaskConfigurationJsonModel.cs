namespace Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Design.JsonModels;

/// <summary>
/// Represents HumanApprovalTaskConfigurationJsonModel.
/// </summary>
public sealed class HumanApprovalTaskConfigurationJsonModel : TaskConfigurationJsonModel
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HumanApprovalTaskConfigurationJsonModel"/> class.
    /// </summary>
    public HumanApprovalTaskConfigurationJsonModel()
    {
        Type = "humanApproval";
    }
}
