using Krackend.Sagas.Orchestrations.WebUI.Shell.Navigation;

namespace Krackend.Sagas.Orchestrations.ControlPlane.WebUI.Design.Navigation;

public sealed class DesignNavigationContributor : IOrchestratorNavigationContributor
{
    public IReadOnlyCollection<OrchestratorNavigationItem> GetItems()
    {
        return
        [
            new OrchestratorNavigationItem
            {
                Label = "Overview",
                Area = "OrchestratorDesign",
                Page = "/Index",
                Order = 0
            },
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
            },
            new OrchestratorNavigationItem
            {
                Label = "Metadata",
                Area = "OrchestratorDesign",
                Page = "/Metadata/Index",
                Order = 30
            }
        ];
    }
}
