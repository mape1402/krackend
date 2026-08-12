using Krackend.Sagas.Orchestrations.WebUI.Shell.Navigation;

namespace Krackend.Sagas.Orchestrations.Runtime.WebUI.Navigation;

public sealed class RuntimeNavigationContributor : IOrchestratorNavigationContributor
{
    public IReadOnlyCollection<OrchestratorNavigationItem> GetItems()
    {
        return
        [
            new OrchestratorNavigationItem { Label = "Instances", Area = "OrchestratorRuntime", Page = "/Instances/Index", Order = 35 },
            new OrchestratorNavigationItem { Label = "Artifacts", Area = "OrchestratorRuntime", Page = "/Artifacts/Index", Order = 40 }
        ];
    }
}
