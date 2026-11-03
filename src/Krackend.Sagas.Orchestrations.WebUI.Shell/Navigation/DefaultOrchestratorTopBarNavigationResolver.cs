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

    /// <summary>Initializes the resolver.</summary>
    public DefaultOrchestratorTopBarNavigationResolver(
        IUrlHelperFactory urlHelperFactory,
        IOptions<OrchestratorTopBarNavigationOptions> options)
    {
        ArgumentNullException.ThrowIfNull(urlHelperFactory);
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
                Label = "Back"
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

        return new ZoorceTopBarBackOptions { Visible = true, Label = "Back" };
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
