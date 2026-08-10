namespace Krackend.Sagas.Orchestrations.Design.Storage.SqlServer.JsonModels;

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
