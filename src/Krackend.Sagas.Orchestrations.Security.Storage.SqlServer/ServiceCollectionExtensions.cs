using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Krackend.Sagas.Orchestrations.Security.Storage.SqlServer.Infrastructure;
using Krackend.Sagas.Orchestrations.Security.Storage.SqlServer.Repositories;

namespace Krackend.Sagas.Orchestrations.Security.Storage.SqlServer;

/// <summary>
/// Registers security storage services for SQL Server.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds security storage dependencies.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <param name="configureDbContext">DbContext configuration callback.</param>
    /// <returns>Configured service collection.</returns>
    public static IServiceCollection AddOrchestratorSecurityStorageSqlServer(
        this IServiceCollection services,
        Action<DbContextOptionsBuilder> configureDbContext)
    {
        services.AddDbContext<SecurityStorageDbContext>(configureDbContext);
        services.AddScoped<ITeamRepository, TeamRepository>();
        services.AddScoped<ITeamMemberRepository, TeamMemberRepository>();
        return services;
    }
}
