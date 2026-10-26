using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Krackend.Sagas.Orchestrations.WolfAuth;

/// <summary>
/// Maps the Krackend login shell and authentication challenge endpoints.
/// </summary>
public static class ApplicationBuilderExtensions
{
    /// <summary>
    /// Redirects anonymous browser requests to the Krackend login shell when WolfAuth integration is enabled.
    /// </summary>
    public static IApplicationBuilder UseKrackendWolfAuthLoginGate(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        if (app.ApplicationServices.GetService<IKrackendWolfAuthRegistration>() is null)
        {
            return app;
        }

        return app.Use(async (context, next) =>
        {
            if (ShouldShowLogin(context, app.ApplicationServices.GetRequiredService<IOptions<KrackendWolfAuthOptions>>().Value))
            {
                var options = app.ApplicationServices.GetRequiredService<IOptions<KrackendWolfAuthOptions>>().Value;
                var returnUrl = context.Request.PathBase + context.Request.Path + context.Request.QueryString;
                context.Response.Redirect(QueryHelpers.AddQueryString(options.LoginPath, options.ReturnUrlParameter, returnUrl));
                return;
            }

            await next(context);
        });
    }

    /// <summary>
    /// Maps the authentication challenge and logout endpoints when WolfAuth integration is enabled.
    /// </summary>
    public static IEndpointRouteBuilder MapKrackendWolfAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        if (endpoints.ServiceProvider.GetService<IKrackendWolfAuthRegistration>() is null)
        {
            return endpoints;
        }

        var options = endpoints.ServiceProvider.GetRequiredService<IOptions<KrackendWolfAuthOptions>>().Value;

        endpoints.MapGet(
                options.ChallengePath,
                (HttpContext context, IOptions<KrackendWolfAuthOptions> options) =>
                    Challenge(context, options.Value))
            .AllowAnonymous()
            .WithName("KrackendWolfAuthChallenge");

        endpoints.MapGet(
                options.LogoutPath,
                (HttpContext context, IOptions<KrackendWolfAuthOptions> options) =>
                    SignOut(context, options.Value))
            .WithName("KrackendWolfAuthLogout");

        return endpoints;
    }

    private static bool ShouldShowLogin(HttpContext context, KrackendWolfAuthOptions options)
    {
        if (context.User.Identity?.IsAuthenticated == true ||
            !HttpMethods.IsGet(context.Request.Method) ||
            IsBypassPath(context.Request.Path, options))
        {
            return false;
        }

        var accept = context.Request.Headers.Accept.ToString();
        return string.IsNullOrWhiteSpace(accept) ||
            accept.Contains("text/html", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsBypassPath(PathString path, KrackendWolfAuthOptions options)
    {
        return path.StartsWithSegments(options.LoginPath, StringComparison.OrdinalIgnoreCase) ||
            path.StartsWithSegments(options.ChallengePath, StringComparison.OrdinalIgnoreCase) ||
            path.StartsWithSegments(options.LogoutPath, StringComparison.OrdinalIgnoreCase) ||
            path.StartsWithSegments("/_orchestrator/status", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWithSegments("/Error", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWithSegments("/_content", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWithSegments("/healthz", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase) ||
            path.Value?.Equals("/favicon.ico", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static IResult Challenge(HttpContext context, KrackendWolfAuthOptions options)
    {
        var returnUrl = NormalizeLocalReturnUrl(
            context.Request.Query[options.ReturnUrlParameter].ToString());
        var properties = new AuthenticationProperties
        {
            RedirectUri = returnUrl
        };

        return options.ChallengeSchemes.Count == 0
            ? Results.Challenge(properties)
            : Results.Challenge(properties, options.ChallengeSchemes);
    }

    private static IResult SignOut(HttpContext context, KrackendWolfAuthOptions options)
    {
        var returnUrl = NormalizeLocalReturnUrl(
            context.Request.Query[options.ReturnUrlParameter].ToString());
        var properties = new AuthenticationProperties
        {
            RedirectUri = returnUrl
        };

        return options.SignOutSchemes.Count == 0
            ? Results.SignOut(properties)
            : Results.SignOut(properties, options.SignOutSchemes);
    }

    private static string NormalizeLocalReturnUrl(string returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl) ||
            !Uri.TryCreate(returnUrl, UriKind.Relative, out var uri) ||
            returnUrl.StartsWith("//", StringComparison.Ordinal))
        {
            return "/";
        }

        return uri.ToString();
    }

}
