using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using System.Text.Json.Serialization;

namespace Krackend.Sagas.Orchestrations.Design.Storage.SqlServer.JsonModels;

/// <summary>
/// Represents RetryPolicyJsonModel.
/// </summary>
public sealed class RetryPolicyJsonModel
{
    /// <summary>
    /// Gets or sets MaxRetries.
    /// </summary>
    public int MaxRetries { get; set; }
    /// <summary>
    /// Gets or sets StrategyType.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public RetryStrategyType StrategyType { get; set; }
    /// <summary>
    /// Gets or sets Strategy.
    /// </summary>
    public RetryStrategyEnvelopeJsonModel Strategy { get; set; }
    /// <summary>
    /// Gets or sets RetryableErrorCodes.
    /// </summary>
    public List<string> RetryableErrorCodes { get; set; } = new();
    /// <summary>
    /// Gets or sets StopOnNonRetryableError.
    /// </summary>
    public bool StopOnNonRetryableError { get; set; }
}
