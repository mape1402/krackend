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
    private readonly IUrlHelperFactory _urlHelperFactory;
    private readonly IOptions<OrchestratorTopBarNavigationOptions> _options;

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
                Label = string.IsNullOrWhiteSpace(configured.Label) ? "Back" : configured.Label.Trim(),
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

        var target = ResolveDefault(area, page, values);
        if (target is null)
        {
            return null;
        }

        var routeValues = new RouteValueDictionary(target.Value.Values) { ["area"] = target.Value.Area };
        var url = _urlHelperFactory.GetUrlHelper(viewContext).Page(target.Value.Page, routeValues);
        return string.IsNullOrWhiteSpace(url)
            ? null
            : new ZoorceTopBarBackOptions { Visible = true, Label = target.Value.Label, Url = url };
    }

    private static BackTarget? ResolveDefault(string area, string page, RouteValueDictionary values)
    {
        if (Matches(area, page, "OrchestratorDesign", "/Orchestrations/Create"))
        {
            return new("OrchestratorDesign", "/Orchestrations/Index", "Back to orchestrations");
        }

        if (Matches(area, page, "OrchestratorDesign", "/Orchestrations/Details"))
        {
            return new("OrchestratorDesign", "/Orchestrations/Index", "Back to orchestrations");
        }

        if (Matches(area, page, "OrchestratorDesign", "/OrchestrationVersions/Details") && Has(values, "orchestrationId"))
        {
            return new("OrchestratorDesign", "/Orchestrations/Details", "Back to orchestration", new { orchestrationId = Value(values, "orchestrationId") });
        }

        if (Matches(area, page, "OrchestratorDesign", "/OrchestrationStages/Details") &&
            Has(values, "orchestrationId") && Has(values, "versionId"))
        {
            return new("OrchestratorDesign", "/OrchestrationVersions/Details", "Back to roadmap", new
            {
                orchestrationId = Value(values, "orchestrationId"),
                versionId = Value(values, "versionId")
            });
        }

        if (Matches(area, page, "OrchestratorDesign", "/Reviews/Details"))
        {
            return new("OrchestratorDesign", "/Reviews/Index", "Back to reviews");
        }

        if (Matches(area, page, "OrchestratorDistribution", "/ArtifactReleases/Index") && Has(values, "orchestrationId"))
        {
            return new("OrchestratorDistribution", "/ArtifactReleases/Index", "Back to artifacts");
        }

        if (Matches(area, page, "OrchestratorDistribution", "/Promotions/Index") && Has(values, "orchestrationId"))
        {
            return new("OrchestratorDistribution", "/Promotions/Index", "Back to releases");
        }

        if (Matches(area, page, "OrchestratorRuntime", "/Artifacts/Index") && Has(values, "orchestrationKey"))
        {
            return new("OrchestratorRuntime", "/Artifacts/Index", "Back to runtime artifacts");
        }

        if (Matches(area, page, "OrchestratorRuntime", "/DesignNodes/PendingReleases"))
        {
            return new("OrchestratorRuntime", "/DesignNodes/Index", "Back to design nodes");
        }

        if (Matches(area, page, "OrchestratorRuntime", "/Instances/History"))
        {
            return new("OrchestratorRuntime", "/Instances/Index", "Back to instances");
        }

        return null;
    }

    private static bool Matches(string area, string page, string expectedArea, string expectedPage)
        => string.Equals(area, expectedArea, StringComparison.OrdinalIgnoreCase)
            && string.Equals(page, expectedPage, StringComparison.OrdinalIgnoreCase);

    private static bool Has(RouteValueDictionary values, string key)
        => !string.IsNullOrWhiteSpace(Value(values, key));

    private static string Value(RouteValueDictionary values, string key)
        => values.TryGetValue(key, out var value) ? value?.ToString() ?? string.Empty : string.Empty;

    private readonly record struct BackTarget(string Area, string Page, string Label, object Values = null);
}
