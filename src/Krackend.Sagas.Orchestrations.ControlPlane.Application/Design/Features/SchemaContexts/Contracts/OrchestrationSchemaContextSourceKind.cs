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
    Variables = 3
}
