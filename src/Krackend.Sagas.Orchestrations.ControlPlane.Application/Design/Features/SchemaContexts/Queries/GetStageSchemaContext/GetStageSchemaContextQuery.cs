namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Represents a request for the schema context available before a stage starts.
/// </summary>
/// <param name="OrchestrationVersionId">Orchestration version identifier.</param>
/// <param name="StageDefinitionId">Stage definition identifier.</param>
public sealed record GetStageSchemaContextQuery(
    string OrchestrationVersionId,
    string StageDefinitionId);
