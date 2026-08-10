using Krackend.Sagas.Orchestrations.WebUI.Shell.Navigation;

namespace Krackend.Sagas.Orchestrations.Distribution.WebUI.Navigation;

public sealed class DistributionNavigationContributor : IOrchestratorNavigationContributor
{
    public IReadOnlyCollection<OrchestratorNavigationItem> GetItems()
    {
        return
        [
            new OrchestratorNavigationItem { Label = "Environments", Area = "OrchestratorDistribution", Page = "/Environments/Index", Order = 20 },
            new OrchestratorNavigationItem { Label = "Runtime Nodes", Area = "OrchestratorDistribution", Page = "/RuntimeNodes/Index", Order = 21 },
            new OrchestratorNavigationItem { Label = "Artifacts", Area = "OrchestratorDistribution", Page = "/ArtifactReleases/Index", Order = 22 },
            new OrchestratorNavigationItem { Label = "Releases", Area = "OrchestratorDistribution", Page = "/Promotions/Index", Order = 23 }
        ];
    }
}

