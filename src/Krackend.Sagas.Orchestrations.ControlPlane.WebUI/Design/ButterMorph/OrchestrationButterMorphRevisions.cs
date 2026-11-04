namespace Krackend.Sagas.Orchestrations.ControlPlane.WebUI.Design.ButterMorph;

using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;

internal static class OrchestrationButterMorphRevisions
{
    public static string ForDesign(string dsl)
        => OrchestrationDesignerRevision.FromDsl(dsl);

    public static bool HasExpectedRevisions(string schema, string design)
        => !string.IsNullOrWhiteSpace(schema) || !string.IsNullOrWhiteSpace(design);

    public static bool Matches(string expectedSchema, string expectedDesign, string schema, string design)
        => !string.IsNullOrWhiteSpace(expectedSchema) && !string.IsNullOrWhiteSpace(expectedDesign)
           && !string.IsNullOrWhiteSpace(schema) && !string.IsNullOrWhiteSpace(design)
           && string.Equals(expectedSchema, schema, StringComparison.Ordinal)
           && string.Equals(expectedDesign, design, StringComparison.Ordinal);
}
