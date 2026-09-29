namespace Krackend.Sagas.Orchestrations.ControlPlane.WebUI.Design.ButterMorph;

using global::ButterMorph.Web.Razor;
using Krackend.Sagas.Orchestrations.WebUI.Shell;

/// <summary>
/// Maps Krackend Web UI theme options into ButterMorph designer theme options.
/// </summary>
internal interface IOrchestrationButterMorphThemeMapper
{
    /// <summary>
    /// Copies Krackend theme values into the ButterMorph designer theme.
    /// </summary>
    /// <param name="source">Krackend theme options.</param>
    /// <param name="target">ButterMorph designer theme options.</param>
    void Apply(OrchestratorWebUIThemeOptions source, ButterMorphDesignerThemeOptions target);
}
