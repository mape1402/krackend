using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Zoorce.Web.UI.Abstractions.UserContext;

namespace Krackend.Sagas.Orchestrations.WebUI.Shell.Authentication;

/// <summary>Supplies the optional host-authenticated user to the Zoorce shell.</summary>
public sealed class ZoorceOrchestratorUserMenuProvider : IUserMenuProvider
{
    private readonly IHttpContextAccessor _http;

    public ZoorceOrchestratorUserMenuProvider(IHttpContextAccessor http)
        => _http = http ?? throw new ArgumentNullException(nameof(http));

    public UserMenuProfile GetCurrentUser()
    {
        var context = _http.HttpContext;
        var principal = context?.User;
        if (context is null || principal?.Identity?.IsAuthenticated != true ||
            context.RequestServices.GetService<IOrchestratorWebUIAuthenticationRegistration>() is null)
            return null;

        var email = principal.FindFirst("preferred_username")?.Value
            ?? principal.FindFirst("upn")?.Value
            ?? principal.FindFirst(ClaimTypes.Email)?.Value
            ?? principal.FindFirst("email")?.Value;
        var name = principal.FindFirst("name")?.Value
            ?? principal.FindFirst(ClaimTypes.Name)?.Value
            ?? principal.Identity.Name;
        if (string.IsNullOrWhiteSpace(name)) name = string.IsNullOrWhiteSpace(email) ? "Signed in" : email;

        return new UserMenuProfile(name, email, Actions:
            [new UserMenuAction("Sign out", $"{context.Request.PathBase}/auth/logout?returnUrl=/")]);
    }
}
