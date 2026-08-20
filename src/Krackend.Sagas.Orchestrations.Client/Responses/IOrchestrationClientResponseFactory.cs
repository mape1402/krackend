namespace Krackend.Sagas.Orchestrations.Client.Responses;

using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Responses;

/// <summary>
/// Builds runtime task response envelopes from Spider pipeline results.
/// </summary>
public interface IOrchestrationClientResponseFactory
{
    /// <summary>
    /// Builds a successful task response envelope.
    /// </summary>
    RuntimeTaskResponseEnvelope Success(
        InstanceMetadata metadata,
        Type requestType,
        Type responseType,
        JsonNode payload,
        TimeSpan? executionTime);

    /// <summary>
    /// Builds a failed task response envelope.
    /// </summary>
    RuntimeTaskResponseEnvelope Failure(
        InstanceMetadata metadata,
        Type requestType,
        string reason,
        Exception exception,
        TimeSpan? executionTime);
}
