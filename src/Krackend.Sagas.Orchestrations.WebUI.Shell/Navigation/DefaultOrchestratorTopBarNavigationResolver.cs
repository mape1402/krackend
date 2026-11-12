using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using Zoorce.Web.UI.Layout.Components;

namespace Krackend.Sagas.Orchestrations.WebUI.Shell.Navigation;

/// <summary>Provides the default contextual hierarchy used by Krackend screens.</summary>
public sealed class DefaultOrchestratorTopBarNavigationResolver : IOrchestratorTopBarNavigationResolver
{
    private readonly IOptions<OrchestratorTopBarNavigationOptions> _options;
    private readonly IUrlHelperFactory _urlHelperFactory;

    /// <summary>Initializes the resolver.</summary>
    public DefaultOrchestratorTopBarNavigationResolver(
        IUrlHelperFactory urlHelperFactory,
        IOptions<OrchestratorTopBarNavigationOptions> options)
    {
        _urlHelperFactory = urlHelperFactory ?? throw new ArgumentNullException(nameof(urlHelperFactory));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public ZoorceTopBarBackOptions Resolve(ViewContext viewContext)
    {
        ArgumentNullException.ThrowIfNull(viewContext);
        var area = viewContext.RouteData.Values["area"]?.ToString() ?? string.Empty;
        var page = viewContext.RouteData.Values["page"]?.ToString() ?? string.Empty;
        var key = $"{area}:{page}";
        var options = _options.Value;

        if (options.Overrides.TryGetValue(key, out var configured))
        {
            return new ZoorceTopBarBackOptions
            {
                Visible = configured.Visible,
                Label = string.IsNullOrWhiteSpace(configured.Label) ? "Back" : configured.Label,
                Url = configured.Url?.Trim()
            };
        }

        if (!options.EnableContextualBackNavigation)
        {
            return null;
        }

        var values = new RouteValueDictionary(viewContext.RouteData.Values);
        foreach (var query in viewContext.HttpContext.Request.Query)
        {
            if (!values.ContainsKey(query.Key)) values[query.Key] = query.Value.ToString();
        }

        if (!HasDefaultBackAction(area, page, values))
        {
            return null;
        }

        var url = _urlHelperFactory.GetUrlHelper(viewContext);
        var requested = viewContext.HttpContext.Request.Query["returnUrl"].ToString();
        var pathBase = viewContext.HttpContext.Request.PathBase.Value ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(requested) && url.IsLocalUrl(requested)
            && (string.IsNullOrEmpty(pathBase) || string.Equals(requested, pathBase, StringComparison.OrdinalIgnoreCase)
                || requested.StartsWith(pathBase + "/", StringComparison.OrdinalIgnoreCase)
                || requested.StartsWith(pathBase + "?", StringComparison.OrdinalIgnoreCase)))
            return new ZoorceTopBarBackOptions { Visible = true, Label = "Back", Url = requested };

        string Page(string targetArea, string targetPage, object routeValues = null)
        {
            var route = new RouteValueDictionary(routeValues) { ["area"] = targetArea };
            return url.Page(targetPage, null, route);
        }
        var destination = (area, page) switch
        {
            ("OrchestratorDesign", "/Orchestrations/Create" or "/Orchestrations/Details") => Page(area, "/Orchestrations/Index"),
            ("OrchestratorDesign", "/OrchestrationVersions/Details") => Page(area, "/Orchestrations/Details", new { orchestrationId = Value(values, "orchestrationId") }),
            ("OrchestratorDesign", "/OrchestrationStages/Details") => Page(area, "/OrchestrationVersions/Details", new { orchestrationId = Value(values, "orchestrationId"), versionId = Value(values, "versionId") }),
            ("OrchestratorDesign", "/Reviews/Details") => Page(area, "/Reviews/Index"),
            ("OrchestratorDistribution", "/ArtifactReleases/Index") => Page(area, "/ArtifactReleases/Index"),
            ("OrchestratorDistribution", "/Promotions/Index") => Page(area, "/Promotions/Index"),
            ("OrchestratorDistribution", "/Assignments/Index") => Page(area, "/Assignments/Index"),
            ("OrchestratorRuntime", "/Artifacts/Index") => Page(area, "/Artifacts/Index"),
            ("OrchestratorRuntime", "/DesignNodes/PendingReleases") => Page(area, "/DesignNodes/Index"),
            ("OrchestratorRuntime", "/Instances/History") => Page(area, "/Instances/Index"),
            ("OrchestratorRuntime", "/Instances/Detail") => Page(area, "/Instances/Index"),
            _ => null
        };
        return string.IsNullOrEmpty(destination) ? null : new ZoorceTopBarBackOptions { Visible = true, Label = "Back", Url = destination };
    }

    private static bool HasDefaultBackAction(string area, string page, RouteValueDictionary values)
    {
        if (Matches(area, page, "OrchestratorDesign", "/Orchestrations/Create"))
        {
            return true;
        }

        if (Matches(area, page, "OrchestratorDesign", "/Orchestrations/Details"))
        {
            return true;
        }

        if (Matches(area, page, "OrchestratorDesign", "/OrchestrationVersions/Details") && Has(values, "orchestrationId"))
        {
            return true;
        }

        if (Matches(area, page, "OrchestratorDesign", "/OrchestrationStages/Details") &&
            Has(values, "orchestrationId") && Has(values, "versionId"))
        {
            return true;
        }

        if (Matches(area, page, "OrchestratorDesign", "/Reviews/Details"))
        {
            return true;
        }

        if (Matches(area, page, "OrchestratorDistribution", "/ArtifactReleases/Index") && Has(values, "orchestrationId"))
        {
            return true;
        }

        if (Matches(area, page, "OrchestratorDistribution", "/Promotions/Index") && Has(values, "orchestrationId"))
        {
            return true;
        }

        if (Matches(area, page, "OrchestratorDistribution", "/Assignments/Index") && Has(values, "assignmentId"))
        {
            return true;
        }

        if (Matches(area, page, "OrchestratorRuntime", "/Artifacts/Index") && Has(values, "orchestrationKey"))
        {
            return true;
        }

        if (Matches(area, page, "OrchestratorRuntime", "/DesignNodes/PendingReleases"))
        {
            return true;
        }

        if (Matches(area, page, "OrchestratorRuntime", "/Instances/History"))
        {
            return true;
        }

        if (Matches(area, page, "OrchestratorRuntime", "/Instances/Detail"))
        {
            return true;
        }

        return false;
    }

    private static bool Matches(string area, string page, string expectedArea, string expectedPage)
        => string.Equals(area, expectedArea, StringComparison.OrdinalIgnoreCase)
            && string.Equals(page, expectedPage, StringComparison.OrdinalIgnoreCase);

    private static bool Has(RouteValueDictionary values, string key)
        => !string.IsNullOrWhiteSpace(Value(values, key));

    private static string Value(RouteValueDictionary values, string key)
        => values.TryGetValue(key, out var value) ? value?.ToString() ?? string.Empty : string.Empty;

}
