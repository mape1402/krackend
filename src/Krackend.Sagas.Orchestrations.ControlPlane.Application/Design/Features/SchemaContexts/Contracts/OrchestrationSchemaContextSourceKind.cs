namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Identifies the source category exposed to a transformation or validation designer.
/// </summary>
public enum OrchestrationSchemaContextSourceKind
{
    /// <summary>
    /// Source comes from the orchestration trigger payload.
    /// </summary>
    Trigger = 1,

    /// <summary>
    /// Source comes from a previously completed task response.
    /// </summary>
    TaskResponse = 2,

    /// <summary>
    /// Source comes from orchestration variables.
    /// </summary>
    Variables = 3,

    /// <summary>
    /// Source comes from a previously dispatched task request.
    /// </summary>
    TaskRequest = 4,

    /// <summary>
    /// Source comes from transversal orchestration metadata descriptors.
    /// </summary>
    Metadata = 5,

    /// <summary>
    /// Source comes from Krackend trigger metadata.
    /// </summary>
    TriggerMetadata = 6,

    /// <summary>
    /// Source comes from a previous compensation request.
    /// </summary>
    CompensationRequest = 7,

    /// <summary>
    /// Source comes from a previous compensation response.
    /// </summary>
    CompensationResponse = 8
}
