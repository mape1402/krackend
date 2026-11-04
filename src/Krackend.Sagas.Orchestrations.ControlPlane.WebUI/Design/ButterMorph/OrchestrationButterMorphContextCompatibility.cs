namespace Krackend.Sagas.Orchestrations.ControlPlane.WebUI.Design.ButterMorph;

internal static class OrchestrationButterMorphContextCompatibility
{
    // Supports pages already open with the previous context-versioning script.
    public static string Resolve(string context, IOrchestrationButterMorphDesignerContextParser parser)
    {
        if (!parser.TryParseTaskTransformation(context, out _) &&
            !parser.TryParseTaskExecutionCondition(context, out _) &&
            !parser.TryParseTaskCompensationTransformation(context, out _) &&
            !parser.TryParseTaskCompensationExecutionCondition(context, out _) &&
            !parser.TryParseTriggerCompensationTransformation(context, out _) &&
            !parser.TryParseTriggerCompensationExecutionCondition(context, out _) &&
            !parser.TryParseStageExecutionCondition(context, out _))
            return null;

        return string.Join(':', context.Split(':').Take(3));
    }
}
