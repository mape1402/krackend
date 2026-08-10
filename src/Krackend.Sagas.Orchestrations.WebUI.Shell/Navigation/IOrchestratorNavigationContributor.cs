namespace Krackend.Sagas.Orchestrations.WebUI.Shell.Navigation;

public interface IOrchestratorNavigationContributor
{
    IReadOnlyCollection<OrchestratorNavigationItem> GetItems();
}
