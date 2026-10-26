using Microsoft.AspNetCore.Mvc.Rendering;
using Zoorce.Web.UI.Layout.Components;

namespace Krackend.Sagas.Orchestrations.WebUI.Shell.Navigation;

/// <summary>Resolves the optional, host-owned top-bar back action for the current page.</summary>
public interface IOrchestratorTopBarNavigationResolver
{
    /// <summary>Gets the contextual back action for a page, when one is applicable.</summary>
    ZoorceTopBarBackOptions Resolve(ViewContext viewContext);
}
