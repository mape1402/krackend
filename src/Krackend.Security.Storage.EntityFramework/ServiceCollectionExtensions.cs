using Krackend.Security.Storage;
using Krackend.Security.Storage.EntityFramework.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Krackend.Security.Storage.EntityFramework;

/// <summary>
/// Registers Entity Framework storage for Krackend product authorization.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the Entity Framework storage adapter for Krackend security.
    /// </summary>
    /// <param name="services">Service collection to configure.</param>
    /// <param name="configureDbContext">Database context configuration callback.</param>
    /// <returns>Configured service collection.</returns>
    public static IServiceCollection AddKrackendSecurityStorageEntityFramework(
        this IServiceCollection services,
        Action<DbContextOptionsBuilder> configureDbContext)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureDbContext);

        services.AddDbContext<KrackendSecurityDbContext>(configureDbContext);
        services.AddScoped<IKrackendSubjectRepository, EntityFrameworkKrackendSubjectRepository>();
        services.AddScoped<IKrackendRoleAssignmentRepository, EntityFrameworkKrackendRoleAssignmentRepository>();
        services.AddScoped<IKrackendPermissionAssignmentRepository, EntityFrameworkKrackendPermissionAssignmentRepository>();
        services.AddScoped<IKrackendExternalGroupRoleAssignmentRepository, EntityFrameworkKrackendExternalGroupRoleAssignmentRepository>();
        return services;
    }
}
