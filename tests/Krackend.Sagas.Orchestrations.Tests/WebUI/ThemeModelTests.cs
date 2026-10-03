using Krackend.Sagas.Orchestrations.WebUI.Shell;
using Krackend.Sagas.Orchestrations.WebUI.Shell.Areas.OrchestratorShell.Pages;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.Options;

namespace Krackend.Sagas.Orchestrations.Tests.WebUI;

public sealed class ThemeModelTests
{
    [Fact]
    public void ConstructorRejectsMissingOptions()
    {
        Assert.Throws<ArgumentNullException>(() => new ThemeModel(null!));
        Assert.Throws<ArgumentNullException>(() => new ThemeModel(Options.Create<OrchestratorThemeModeCookieOptions>(null!)));
    }

    [Theory]
    [InlineData("DARK", "dark")]
    [InlineData("light", "light")]
    public void OnPostStoresSupportedModesAndRedirectsToLocalUrl(string requestedMode, string storedMode)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Scheme = Uri.UriSchemeHttps;
        var model = CreateModel(httpContext);

        var result = Assert.IsType<LocalRedirectResult>(model.OnPost(requestedMode, "/orchestrator"));

        Assert.Equal("/orchestrator", result.Url);
        var setCookie = httpContext.Response.Headers.SetCookie.ToString();
        Assert.Contains($"krackend-theme={storedMode}", setCookie, StringComparison.Ordinal);
        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", setCookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void OnPostIgnoresUnsupportedModesAndRedirectsExternalUrlsToRoot()
    {
        var httpContext = new DefaultHttpContext();
        var model = CreateModel(httpContext);

        var result = Assert.IsType<LocalRedirectResult>(model.OnPost("sepia", "https://example.test/return"));

        Assert.Equal("/", result.Url);
        Assert.True(httpContext.Response.Headers.SetCookie.Count == 0);
    }

    private static ThemeModel CreateModel(DefaultHttpContext httpContext)
        => new(Options.Create(new OrchestratorThemeModeCookieOptions
        {
            CookieName = "krackend-theme",
            Lifetime = TimeSpan.FromMinutes(10)
        }))
        {
            PageContext = new Microsoft.AspNetCore.Mvc.RazorPages.PageContext
            {
                HttpContext = httpContext
            },
            Url = new LocalOnlyUrlHelper()
        };

    private sealed class LocalOnlyUrlHelper : IUrlHelper
    {
        public ActionContext ActionContext { get; } = new();

        public string? Action(UrlActionContext actionContext)
            => null;

        public string? Content(string? contentPath)
            => contentPath;

        public bool IsLocalUrl(string? url)
            => !string.IsNullOrEmpty(url) && url[0] == '/' && (url.Length == 1 || url[1] != '/' && url[1] != '\\');

        public string? Link(string? routeName, object? values)
            => null;

        public string? RouteUrl(UrlRouteContext routeContext)
            => null;
    }
}
