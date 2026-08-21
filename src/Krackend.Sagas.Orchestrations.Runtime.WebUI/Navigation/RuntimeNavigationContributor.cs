using Krackend.Sagas.Orchestrations.WebUI.Shell.Navigation;

namespace Krackend.Sagas.Orchestrations.Runtime.WebUI.Navigation;

/// <summary>
/// Adds runtime diagnostics entries to the shared orchestration UI navigation.
/// </summary>
public sealed class RuntimeNavigationContributor : IOrchestratorNavigationContributor
{
    /// <summary>
    /// Gets navigation items contributed by the runtime diagnostics UI.
    /// </summary>
    public IReadOnlyCollection<OrchestratorNavigationItem> GetItems()
    {
        return
        [
            new OrchestratorNavigationItem { Label = "Runtime", Area = "OrchestratorRuntime", Page = "/Instances/Index", Order = 35 }
        ];
    }
}
