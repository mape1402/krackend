namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Dispatching;

using System.Text.Json.Nodes;

/// <summary>
/// Represents a technical failure that happened before a command was published.
/// </summary>
public sealed class TaskDispatchPreparationException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TaskDispatchPreparationException"/> class.
    /// </summary>
    /// <param name="errorCode">Normalized orchestration error code.</param>
    /// <param name="message">Error message.</param>
    /// <param name="diagnostics">Technical diagnostics.</param>
    public TaskDispatchPreparationException(
        string errorCode,
        string message,
        IReadOnlyDictionary<string, JsonNode> diagnostics = null)
        : base(message)
    {
        ErrorCode = errorCode;
        Diagnostics = diagnostics ?? new Dictionary<string, JsonNode>();
    }

    /// <summary>
    /// Gets the normalized orchestration error code.
    /// </summary>
    public string ErrorCode { get; }

    /// <summary>
    /// Gets technical diagnostics captured while preparing the dispatch.
    /// </summary>
    public IReadOnlyDictionary<string, JsonNode> Diagnostics { get; }
}
