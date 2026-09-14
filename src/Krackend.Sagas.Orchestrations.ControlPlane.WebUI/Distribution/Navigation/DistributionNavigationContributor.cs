using Krackend.Sagas.Orchestrations.WebUI.Shell.Navigation;

namespace Krackend.Sagas.Orchestrations.ControlPlane.WebUI.Distribution.Navigation;

public sealed class DistributionNavigationContributor : IOrchestratorNavigationContributor
{
    public IReadOnlyCollection<OrchestratorNavigationItem> GetItems()
    {
        return
        [
            new OrchestratorNavigationItem { Label = "Runtime Nodes", Area = "OrchestratorDistribution", Page = "/RuntimeNodes/Index", Order = 20 },
            new OrchestratorNavigationItem { Label = "Artifacts", Area = "OrchestratorDistribution", Page = "/ArtifactReleases/Index", Order = 22 },
            new OrchestratorNavigationItem { Label = "Releases", Area = "OrchestratorDistribution", Page = "/Promotions/Index", Order = 23 }
        ];
    }
}

