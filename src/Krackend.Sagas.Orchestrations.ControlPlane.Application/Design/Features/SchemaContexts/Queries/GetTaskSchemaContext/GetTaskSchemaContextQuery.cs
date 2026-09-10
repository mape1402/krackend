namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Requests the schema context available before a task dispatch.
/// </summary>
/// <param name="OrchestrationVersionId">Orchestration version identifier.</param>
/// <param name="TaskDefinitionId">Task definition identifier.</param>
public sealed record GetTaskSchemaContextQuery(
    string OrchestrationVersionId,
    string TaskDefinitionId);
