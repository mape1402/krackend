namespace Krackend.Sagas.Orchestrations.WebUI.Shell.Navigation;

public sealed class OrchestratorNavigationRegistry
{
    private readonly IEnumerable<IOrchestratorNavigationContributor> _contributors;

    public OrchestratorNavigationRegistry(IEnumerable<IOrchestratorNavigationContributor> contributors)
    {
        _contributors = contributors;
    }

    public IReadOnlyCollection<OrchestratorNavigationItem> GetItems()
    {
        return _contributors
            .SelectMany(x => x.GetItems())
            .OrderBy(x => x.Order)
            .ThenBy(x => x.Label, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
