using Inventories.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Provides dependency injection extensions for persistence.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public static class PersistenceExtensions
    {
        /// <summary>
        /// Registers persistence services using the provided connection string.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configuration">The application configuration.</param>
        /// <returns>The service collection.</returns>
        public static IServiceCollection AddPersistenceDefaults(this IServiceCollection services, IConfiguration configuration)
        {
            var connectionString = configuration.GetConnectionString("Default");

            services.AddDbContext<AppDbContext>(opts =>
            {
                // Uncomment the following line to use SQL Server
                opts.UseSqlServer(connectionString);

                // Uncomment the following line to use PostgreSQL
                //opts.UseNpgsql(connectionString);
            });

            return services;
        }
    }
}
