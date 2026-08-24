namespace Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Design.JsonModels;

/// <summary>
/// Represents FixedRetryStrategyJsonModel.
/// </summary>
public sealed class FixedRetryStrategyJsonModel : RetryStrategyJsonModel
{
    /// <summary>
    /// Gets or sets Delay.
    /// </summary>
    public TimeSpan Delay { get; set; }
}
