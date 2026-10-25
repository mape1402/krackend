using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Zoorce.Web.UI.Abstractions.Navigation;

namespace Krackend.Sagas.Orchestrations.WebUI.Shell.Navigation;

/// <summary>Exposes registered orchestration pages to the Zoorce shell.</summary>
public sealed class ZoorceOrchestratorNavigationProvider : INavigationProvider
{
    private readonly OrchestratorNavigationRegistry _registry;
    private readonly LinkGenerator _links;
    private readonly IHttpContextAccessor _context;

    public ZoorceOrchestratorNavigationProvider(
        OrchestratorNavigationRegistry registry, LinkGenerator links, IHttpContextAccessor context)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _links = links ?? throw new ArgumentNullException(nameof(links));
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public IReadOnlyList<NavigationItem> GetItems()
    {
        var context = _context.HttpContext;
        if (context is null) return [];

        var items = _registry.GetItems();
        var result = new List<NavigationItem>();
        var overview = items.FirstOrDefault(x => x.Area == "OrchestratorDesign" && x.Page == "/Index");
        if (overview is not null)
        {
            var url = _links.GetPathByPage(context, overview.Page, values: new { area = overview.Area });
            if (url is not null) result.Add(new NavigationItem(overview.Label, url, MatchMode: NavigationMatchMode.Exact));
        }

        foreach (var group in new[]
        {
            (Area: "OrchestratorDesign", Label: "Design"),
            (Area: "OrchestratorDistribution", Label: "Distribution"),
            (Area: "OrchestratorRuntime", Label: "Runtime"),
            (Area: "OrchestratorSecurity", Label: "Security")
        })
        {
            var children = items.Where(x => x.Area == group.Area && x != overview)
                .Select(x => (Item: x, Url: _links.GetPathByPage(context, x.Page, values: new { area = x.Area })))
                .Where(x => x.Url is not null)
                .Select(x => new NavigationItem(x.Item.Label, x.Url))
                .ToArray();
            if (children.Length > 0)
                result.Add(new NavigationItem(group.Label, Children: children));
        }

        return result;
    }
}
