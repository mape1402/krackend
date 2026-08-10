using Krackend.Sagas.Orchestrations.WebUI.Shell.Navigation;

namespace Krackend.Sagas.Orchestrations.Security.WebUI.Navigation;

/// <summary>
/// Contributes Security navigation nodes.
/// </summary>
public sealed class SecurityNavigationContributor : IOrchestratorNavigationContributor
{
    /// <inheritdoc />
    public IReadOnlyCollection<OrchestratorNavigationItem> GetItems()
    {
        return
        [
            new OrchestratorNavigationItem
            {
                Label = "Teams",
                Area = "OrchestratorSecurity",
                Page = "/Teams/Index",
                Order = 30
            }
        ];
    }
}
