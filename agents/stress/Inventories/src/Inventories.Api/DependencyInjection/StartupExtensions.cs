using Serilog;
using System.Diagnostics.CodeAnalysis;
using Inventories.Api.DependencyInjection;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Provides startup service configuration extensions.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public static class StartupExtensions
    {
        /// <summary>
        /// Registers default services and middleware dependencies.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configuration">The application configuration.</param>
        /// <param name="environment">The hosting environment.</param>
        /// <returns>The service collection.</returns>
        public static IServiceCollection AddDefaults(this IServiceCollection services, IConfiguration configuration, IWebHostEnvironment environment)
        {
            return services
                .AddMvcDefaults()
                .AddOpenApiDefaults()
                .AddHealthCheckDefaults(configuration)
                .AddPersistenceDefaults(configuration)
                .AddApplicationDefaults()
                // Event sourcing is ready but intentionally opt-in because the service must define streams and event tables.
                // Uncomment this together with .UseEventSourcingProfiles(...) in AddApplicationDefaults.
                // .AddEventSourcingDefaults()
                .AddMessagingDefaults(configuration)
                .AddPipelineDefaults(configuration)
                .AddCustomContainer(configuration);
        }

        /// <summary>
        /// Configures default middleware for the application.
        /// </summary>
        /// <param name="app">The application builder.</param>
        /// <param name="environment">The hosting environment.</param>
        /// <returns>The application builder.</returns>
        public static IApplicationBuilder UseDefaults(this IApplicationBuilder app, IWebHostEnvironment environment)
        {
            app.UseLoggingDefaults(environment);
            app.UseOpenApiDefaults(environment);
            app.UseRoutingDefaults();
            app.UseEndpointDefaults();

            return app;
        }

        private static IApplicationBuilder UseLoggingDefaults(this IApplicationBuilder app, IWebHostEnvironment environment)
        {
            if (environment.IsDevelopment())
                app.UseSerilogRequestLogging();

            return app;
        }

        private static IApplicationBuilder UseRoutingDefaults(this IApplicationBuilder app)
        {
            app.UseHttpsRedirection();
            app.UseRouting();
            app.UseAuthorization();

            return app;
        }

        private static IApplicationBuilder UseEndpointDefaults(this IApplicationBuilder app)
        {
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapHealthCheckEndPoints();
                endpoints.MapControllers();
            });

            return app;
        }

    }
}
