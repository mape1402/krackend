namespace Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;

using System.Text.Json.Nodes;

/// <summary>
/// Carries the technical result of an orchestrated operation through transport metadata.
/// </summary>
public sealed class OrchestrationExecutionResultMetadata
{
    /// <summary>
    /// Gets or sets a value indicating whether the operation completed successfully.
    /// </summary>
    public bool Succeeded { get; set; }

    /// <summary>
    /// Gets or sets the normalized execution status.
    /// </summary>
    public string Status { get; set; }

    /// <summary>
    /// Gets or sets the technical error code when the operation failed.
    /// </summary>
    public string ErrorCode { get; set; }

    /// <summary>
    /// Gets or sets the technical error message when the operation failed.
    /// </summary>
    public string ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets the technical error type when the operation failed.
    /// </summary>
    public string ErrorType { get; set; }

    /// <summary>
    /// Gets or sets the operation start timestamp in UTC.
    /// </summary>
    public DateTime? StartedOnUtc { get; set; }

    /// <summary>
    /// Gets or sets the operation completion timestamp in UTC.
    /// </summary>
    public DateTime? CompletedOnUtc { get; set; }

    /// <summary>
    /// Gets or sets the elapsed execution time in milliseconds.
    /// </summary>
    public long? ExecutionTimeMs { get; set; }

    /// <summary>
    /// Gets or sets the request CLR type name.
    /// </summary>
    public string RequestType { get; set; }

    /// <summary>
    /// Gets or sets the response CLR type name.
    /// </summary>
    public string ResponseType { get; set; }

    /// <summary>
    /// Gets or sets additional technical execution metadata.
    /// </summary>
    public Dictionary<string, JsonNode> Metadata { get; set; } = new();
}
