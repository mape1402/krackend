namespace Krackend.Sagas.Orchestrations.ControlPlane.WebUI.Design.ButterMorph;

/// <summary>
/// Formats and parses ButterMorph designer context keys owned by the orchestration designer.
/// </summary>
public interface IOrchestrationButterMorphDesignerContextParser
{
    /// <summary>
    /// Builds a designer context key for one orchestration task transformation.
    /// </summary>
    /// <param name="orchestrationVersionId">Orchestration version identifier.</param>
    /// <param name="taskDefinitionId">Task definition identifier.</param>
    /// <returns>Designer context key.</returns>
    string FormatTaskTransformation(string orchestrationVersionId, string taskDefinitionId);

    /// <summary>
    /// Parses a designer context key into its orchestration scope.
    /// </summary>
    /// <param name="contextKey">Designer context key.</param>
    /// <param name="context">Parsed context when the operation succeeds.</param>
    /// <returns>True when the context key belongs to an orchestration task transformation.</returns>
    bool TryParseTaskTransformation(string contextKey, out OrchestrationButterMorphDesignerContext context);
}
