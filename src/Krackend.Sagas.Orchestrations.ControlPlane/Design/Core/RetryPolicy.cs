namespace Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Represents retry behavior configuration applied to stage or task execution.
/// </summary>
public sealed class RetryPolicy
{
    /// <summary>
    /// Gets or sets max retries.
    /// </summary>
    public int MaxRetries { get; set; }

    /// <summary>
    /// Gets or sets strategy type.
    /// </summary>
    public required RetryStrategyType StrategyType { get; set; }

    /// <summary>
    /// Gets or sets the retry strategy used to determine how failed operations are retried.
    /// </summary>
    /// <remarks>Use this property to customize the retry behavior for operations that may fail and require
    /// multiple attempts. The specified strategy defines the logic for retrying failed requests, such as the number of
    /// retries, delay intervals, and conditions for retrying. Assigning a custom implementation of IRetryStrategy
    /// allows for fine-grained control over retry policies to suit specific application requirements.</remarks>
    public IRetryStrategy Strategy { get; set; }

    /// <summary>
    /// Gets or sets retryable error codes.
    /// </summary>
    public List<string> RetryableErrorCodes { get; set; } = new();

    /// <summary>
    /// Gets or sets stop on non retryable error.
    /// </summary>
    public bool StopOnNonRetryableError { get; set; }
}
