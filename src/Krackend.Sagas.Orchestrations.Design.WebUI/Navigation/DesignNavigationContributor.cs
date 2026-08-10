using Krackend.Sagas.Orchestrations.WebUI.Shell.Navigation;

namespace Krackend.Sagas.Orchestrations.Design.WebUI.Navigation;

public sealed class DesignNavigationContributor : IOrchestratorNavigationContributor
{
    public IReadOnlyCollection<OrchestratorNavigationItem> GetItems()
    {
        return
        [
            new OrchestratorNavigationItem
            {
                Label = "Orchestrations",
                Area = "OrchestratorDesign",
                Page = "/Orchestrations/Index",
                Order = 10
            },
            new OrchestratorNavigationItem
            {
                Label = "Domains",
                Area = "OrchestratorDesign",
                Page = "/Domains/Index",
                Order = 20
            }
        ];
    }
}
