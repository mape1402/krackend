using System.Security.Claims;
using Krackend.Sagas.Orchestrations.WebUI.Shell.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Krackend.Sagas.Orchestrations.Tests.WebUI;

public sealed class ZoorceOrchestratorUserMenuProviderTests
{
    [Fact]
    public void ConstructorRejectsNullAccessor()
        => Assert.Throws<ArgumentNullException>(() => new ZoorceOrchestratorUserMenuProvider(null!));

    [Fact]
    public void ReturnsNoProfileWithoutRequest()
        => Assert.Null(new ZoorceOrchestratorUserMenuProvider(new HttpContextAccessor()).GetCurrentUser());

    [Fact]
    public void ReturnsNoProfileWhenAuthenticationIsNotRegistered()
    {
        var context = Context(authEnabled: false, authenticated: true);
        Assert.Null(Provider(context).GetCurrentUser());
    }

    [Fact]
    public void ReturnsNoProfileForAnonymousUser()
    {
        var context = Context(authEnabled: true, authenticated: false);
        Assert.Null(Provider(context).GetCurrentUser());
    }

    [Fact]
    public void ReturnsAuthenticatedUserAndPathBaseAwareSignOut()
    {
        var context = Context(true, true, new Claim("name", "Ada"), new Claim("preferred_username", "ada@example.test"));
        context.Request.PathBase = "/workspace";
        var profile = Provider(context).GetCurrentUser();

        Assert.Equal("Ada", profile.DisplayName);
        Assert.Equal("ada@example.test", profile.Email);
        Assert.Equal("/workspace/auth/logout?returnUrl=/", Assert.Single(profile.Actions!).Url);
    }

    [Theory]
    [InlineData("upn")]
    [InlineData(ClaimTypes.Email)]
    [InlineData("email")]
    public void UsesAvailableEmailAsDisplayName(string claimType)
    {
        var profile = Provider(Context(true, true, new Claim(claimType, "user@example.test"))).GetCurrentUser();
        Assert.Equal("user@example.test", profile.DisplayName);
        Assert.Equal("user@example.test", profile.Email);
    }

    [Fact]
    public void UsesFallbackNameWhenNoClaimsAreAvailable()
        => Assert.Equal("Signed in", Provider(Context(true, true)).GetCurrentUser().DisplayName);

    private static ZoorceOrchestratorUserMenuProvider Provider(HttpContext context)
        => new(new HttpContextAccessor { HttpContext = context });

    private static DefaultHttpContext Context(bool authEnabled, bool authenticated, params Claim[] claims)
    {
        var services = new ServiceCollection();
        if (authEnabled) services.AddSingleton(Substitute.For<IOrchestratorWebUIAuthenticationRegistration>());
        return new DefaultHttpContext
        {
            RequestServices = services.BuildServiceProvider(),
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, authenticated ? "test" : null))
        };
    }
}
