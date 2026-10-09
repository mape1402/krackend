using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Krackend.Sagas.Orchestrations.WolfAuth;
using Krackend.Sagas.Orchestrations.WebUI.Shell.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Krackend.Sagas.Orchestrations.Tests.Security;

public sealed class WolfAuthIntegrationTests
{
    [Fact]
    public async Task LoginGateRedirectsAnonymousBrowserRequestsToLoginShell()
    {
        await using var fixture = await WolfAuthFixture.Start();

        using var response = await fixture.Client.GetAsync("/secure?x=1");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/auth/login?returnUrl=%2Fsecure%3Fx%3D1", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task LoginGateDoesNotRenderLoginForNonHtmlRequests()
    {
        await using var fixture = await WolfAuthFixture.Start();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/secure");
        request.Headers.Accept.ParseAdd("application/json");

        using var response = await fixture.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/external-login?returnUrl=%2F", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task LoginGateDoesNotRenderLoginForNonGetRequests()
    {
        await using var fixture = await WolfAuthFixture.Start();

        using var response = await fixture.Client.PostAsync("/secure", null);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/external-login?returnUrl=%2F", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public void LoginDecisionTreatsMissingIdentityAsAnonymousBrowserRequest()
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal()
        };
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = "/secure";
        context.Request.Headers.Accept = "text/html";

        var shouldShowLogin = InvokeShouldShowLogin(context, new KrackendWolfAuthOptions());

        Assert.True(shouldShowLogin);
    }

    [Theory]
    [InlineData("/auth/logout")]
    [InlineData("/_content/Krackend.Sagas.Orchestrations.WebUI.Shell/css/orchestrator-shell.css")]
    [InlineData("/health")]
    [InlineData("/healthz")]
    [InlineData("/api/orchestrations")]
    [InlineData("/favicon.ico")]
    public async Task LoginGateDoesNotRenderLoginForBypassPaths(string path)
    {
        await using var fixture = await WolfAuthFixture.Start();

        using var response = await fixture.Client.GetAsync(path);

        Assert.False(response.Headers.Location?.OriginalString.StartsWith("/auth/login", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public void BypassPathTreatsEmptyPathAsNonBypass()
    {
        var isBypass = InvokeIsBypassPath(default, new KrackendWolfAuthOptions());

        Assert.False(isBypass);
    }

    [Fact]
    public async Task LoginShellRendersConfiguredLoginButton()
    {
        await using var fixture = await WolfAuthFixture.Start(options =>
        {
            options.ApplicationName = "Sample Krackend";
            options.Subtitle = "Authentication required.";
            options.LoginButtonText = "Sign in";
        });

        var html = await fixture.Client.GetStringAsync("/auth/login?returnUrl=/secure");

        Assert.Contains("Sample Krackend", html);
        Assert.Contains("Authentication required.", html);
        Assert.Contains(">Sign in</a>", html);
        Assert.Contains("/auth/login/challenge?returnUrl=%2Fsecure", html);
    }

    [Fact]
    public async Task ChallengeEndpointUsesDefaultAuthenticationSchemeWhenNoSchemeIsConfigured()
    {
        await using var fixture = await WolfAuthFixture.Start();

        using var response = await fixture.Client.GetAsync("/auth/login/challenge?returnUrl=/secure");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/external-login?returnUrl=%2Fsecure", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task ChallengeEndpointUsesConfiguredAuthenticationScheme()
    {
        await using var fixture = await WolfAuthFixture.Start(options =>
            options.ChallengeSchemes.Add(TestAuthenticationDefaults.Scheme));

        using var response = await fixture.Client.GetAsync("/auth/login/challenge?returnUrl=/secure");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/external-login?returnUrl=%2Fsecure", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task LogoutEndpointUsesDefaultSignOutSchemeWhenNoSchemeIsConfigured()
    {
        await using var fixture = await WolfAuthFixture.Start(authenticated: true);

        using var response = await fixture.Client.GetAsync("/auth/logout?returnUrl=/secure");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/signed-out?returnUrl=%2Fsecure", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task LogoutEndpointUsesConfiguredSignOutScheme()
    {
        await using var fixture = await WolfAuthFixture.Start(
            options => options.SignOutSchemes.Add(TestAuthenticationDefaults.Scheme),
            authenticated: true);

        using var response = await fixture.Client.GetAsync("/auth/logout?returnUrl=/secure");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/signed-out?returnUrl=%2Fsecure", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task AuthenticatedRequestsReachTheApplication()
    {
        await using var fixture = await WolfAuthFixture.Start(authenticated: true);

        var content = await fixture.Client.GetStringAsync("/secure");

        Assert.Equal("ok", content);
    }

    [Fact]
    public async Task IntegrationIsNoOpWhenUseWolfAuthWasNotRegistered()
    {
        await using var fixture = await WolfAuthFixture.Start(
            authenticated: false,
            registerKrackendWolfAuth: false);

        var content = await fixture.Client.GetStringAsync("/secure");

        Assert.Equal("ok", content);
    }

    [Fact]
    public async Task IntegrationIsNoOpWhenWolfAuthIsDisabledByOptions()
    {
        await using var fixture = await WolfAuthFixture.Start(options => options.Enabled = false);

        var content = await fixture.Client.GetStringAsync("/secure");
        using var login = await fixture.Client.GetAsync("/auth/login");

        Assert.Equal("ok", content);
        Assert.Equal(HttpStatusCode.NotFound, login.StatusCode);
    }

    [Fact]
    public async Task ConfigurationOverloadAppliesAdditionalOptionConfiguration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["LoginButtonText"] = "Configured sign in"
            })
            .Build();
        await using var fixture = await WolfAuthFixture.Start(
            wolfAuthConfiguration: configuration,
            configureOptions: options => options.LoginButtonText = "Overridden sign in");

        var html = await fixture.Client.GetStringAsync("/auth/login");

        Assert.Contains(">Overridden sign in</a>", html);
        Assert.DoesNotContain(">Configured sign in</a>", html);
    }

    [Fact]
    public async Task IntegrationIsNoOpWhenWolfAuthIsDisabledByConfiguration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Enabled"] = "false"
            })
            .Build();
        await using var fixture = await WolfAuthFixture.Start(
            authenticated: false,
            wolfAuthConfiguration: configuration);

        var content = await fixture.Client.GetStringAsync("/secure");
        using var login = await fixture.Client.GetAsync("/auth/login");

        Assert.Equal("ok", content);
        Assert.Equal(HttpStatusCode.NotFound, login.StatusCode);
    }

    [Fact]
    public async Task EnabledIntegrationRegistersShellAuthenticationMarker()
    {
        await using var fixture = await WolfAuthFixture.Start(authenticated: true);

        Assert.NotNull(fixture.App.Services.GetService<IKrackendWolfAuthRegistration>());
        Assert.NotNull(fixture.App.Services.GetService<IOrchestratorWebUIAuthenticationRegistration>());
    }

    private static bool InvokeShouldShowLogin(HttpContext context, KrackendWolfAuthOptions options)
    {
        var method = typeof(Krackend.Sagas.Orchestrations.WolfAuth.ApplicationBuilderExtensions).GetMethod(
            "ShouldShowLogin",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(method);
        return Assert.IsType<bool>(method.Invoke(null, [context, options]));
    }

    private static bool InvokeIsBypassPath(PathString path, KrackendWolfAuthOptions options)
    {
        var method = typeof(Krackend.Sagas.Orchestrations.WolfAuth.ApplicationBuilderExtensions).GetMethod(
            "IsBypassPath",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(method);
        return Assert.IsType<bool>(method.Invoke(null, [path, options]));
    }

    private sealed class WolfAuthFixture(WebApplication app, HttpClient client) : IAsyncDisposable
    {
        public WebApplication App { get; } = app;

        public HttpClient Client { get; } = client;

        public static async Task<WolfAuthFixture> Start(
            Action<KrackendWolfAuthOptions>? configureOptions = null,
            bool authenticated = false,
            bool registerKrackendWolfAuth = true,
            IConfiguration? wolfAuthConfiguration = null)
        {
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Services.AddAuthentication(TestAuthenticationDefaults.Scheme)
                .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                    TestAuthenticationDefaults.Scheme,
                    options => { });
            builder.Services.AddAuthorization();

            builder.Services.Configure<TestAuthenticationState>(options => options.IsAuthenticated = authenticated);

            if (registerKrackendWolfAuth)
            {
                if (wolfAuthConfiguration is null)
                {
                    builder.Services.UseWolfAuth(configureOptions);
                }
                else
                {
                    builder.Services.UseWolfAuth(wolfAuthConfiguration, configureOptions);
                }
            }

            var app = builder.Build();
            app.UseAuthentication();
            app.UseKrackendWolfAuthLoginGate();
            app.UseAuthorization();
            app.MapKrackendWolfAuthEndpoints();
            app.MapGet("/secure", () => "ok");

            await app.StartAsync();
            return new WolfAuthFixture(app, app.GetTestClient());
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await App.DisposeAsync();
        }
    }

    private static class TestAuthenticationDefaults
    {
        public const string Scheme = "test";
    }

    private sealed class TestAuthenticationState
    {
        public bool IsAuthenticated { get; set; }
    }

    private sealed class TestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IOptions<TestAuthenticationState> state)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder), IAuthenticationSignOutHandler
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!state.Value.IsAuthenticated)
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var identity = new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, "user-1"),
                    new Claim(ClaimTypes.Name, "Test User"),
                    new Claim(ClaimTypes.Email, "test@example.com")
                ],
                Scheme.Name);
            var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }

        protected override Task HandleChallengeAsync(AuthenticationProperties properties)
        {
            var returnUrl = properties.RedirectUri ?? "/";
            Response.Redirect($"/external-login?returnUrl={WebUtility.UrlEncode(returnUrl)}");
            return Task.CompletedTask;
        }

        public Task SignOutAsync(AuthenticationProperties? properties)
        {
            var returnUrl = properties?.RedirectUri ?? "/";
            Response.Redirect($"/signed-out?returnUrl={WebUtility.UrlEncode(returnUrl)}");
            return Task.CompletedTask;
        }
    }
}
