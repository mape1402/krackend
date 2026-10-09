using Krackend.Sagas.Orchestrations.WebUI.Shell.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Krackend.Sagas.Orchestrations.WolfAuth;

/// <summary>
/// Registers Krackend integration services that sit on top of host-configured WolfAuth authentication.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Enables Krackend's WolfAuth integration. The host must configure WolfAuth and ASP.NET Core authentication separately.
    /// </summary>
    public static IServiceCollection UseWolfAuth(
        this IServiceCollection services,
        Action<KrackendWolfAuthOptions> configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new KrackendWolfAuthOptions();
        configure?.Invoke(options);

        services.AddOptions<KrackendWolfAuthOptions>();
        if (configure is not null)
        {
            services.Configure(configure);
        }

        RegisterEnabledIntegration(services, options.Enabled);

        return services;
    }

    /// <summary>
    /// Enables Krackend's WolfAuth integration from configuration. The host must configure WolfAuth and ASP.NET Core authentication separately.
    /// </summary>
    public static IServiceCollection UseWolfAuth(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<KrackendWolfAuthOptions> configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var options = new KrackendWolfAuthOptions();
        configuration.Bind(options);
        configure?.Invoke(options);

        services.AddOptions<KrackendWolfAuthOptions>();
        services.Configure<KrackendWolfAuthOptions>(configuration);
        if (configure is not null)
        {
            services.Configure(configure);
        }

        RegisterEnabledIntegration(services, options.Enabled);

        return services;
    }

    private static void RegisterEnabledIntegration(IServiceCollection services, bool enabled)
    {
        if (!enabled)
        {
            return;
        }

        services.TryAddSingleton<IKrackendWolfAuthRegistration, KrackendWolfAuthRegistration>();
        services.TryAddSingleton<IOrchestratorWebUIAuthenticationRegistration, KrackendWebUIAuthenticationRegistration>();
        services.AddAuthorization(options =>
        {
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();
        });
    }

    private sealed class KrackendWebUIAuthenticationRegistration : IOrchestratorWebUIAuthenticationRegistration;
}
