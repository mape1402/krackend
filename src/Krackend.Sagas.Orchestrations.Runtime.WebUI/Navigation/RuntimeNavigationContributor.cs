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
            new OrchestratorNavigationItem { Label = "Dashboard", Area = "OrchestratorRuntime", Page = "/Dashboard/Index", Section = "Activity", Order = 34 },
            new OrchestratorNavigationItem { Label = "Instances", Area = "OrchestratorRuntime", Page = "/Instances/Index", Section = "Activity", Order = 35 },
            new OrchestratorNavigationItem { Label = "Statistics", Area = "OrchestratorRuntime", Page = "/Statistics/Index", Section = "Activity", Order = 36 },
            new OrchestratorNavigationItem { Label = "History", Area = "OrchestratorRuntime", Page = "/Instances/History", Section = "Activity", Order = 37 },
            new OrchestratorNavigationItem { Label = "Artifacts", Area = "OrchestratorRuntime", Page = "/Artifacts/Index", Section = "Configuration", Order = 38 },
            new OrchestratorNavigationItem { Label = "Design Nodes", Area = "OrchestratorRuntime", Page = "/DesignNodes/Index", Section = "Configuration", Order = 39 }
        ];
    }
}
