using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Krackend.Sagas.Orchestrations.WolfAuth;
using Krackend.Sagas.Orchestrations.WebUI.Shell.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
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
    public async Task ChallengeEndpointUsesConfiguredAuthenticationScheme()
    {
        await using var fixture = await WolfAuthFixture.Start(options =>
            options.ChallengeSchemes.Add(TestAuthenticationDefaults.Scheme));

        using var response = await fixture.Client.GetAsync("/auth/login/challenge?returnUrl=/secure");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/external-login?returnUrl=%2Fsecure", response.Headers.Location?.OriginalString);
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
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
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
    }
}
