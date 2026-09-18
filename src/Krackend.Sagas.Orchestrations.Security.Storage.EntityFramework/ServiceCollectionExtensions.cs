#nullable enable

using Krackend.Sagas.Orchestrations.Security.Storage;
using Krackend.Sagas.Orchestrations.Security.Storage.EntityFramework.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Krackend.Sagas.Orchestrations.Security.Storage.EntityFramework;

/// <summary>
/// Registers Entity Framework storage for Krackend orchestration authorization.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the Entity Framework storage adapter for Krackend orchestration security.
    /// </summary>
    /// <param name="services">Service collection to configure.</param>
    /// <param name="configureDbContext">Database context configuration callback.</param>
    /// <param name="configureStorage">Optional callback used to customize the portable Entity Framework model.</param>
    /// <returns>Configured service collection.</returns>
    public static IServiceCollection AddKrackendSecurityStorageEntityFramework(
        this IServiceCollection services,
        Action<DbContextOptionsBuilder> configureDbContext,
        Action<KrackendSecurityEntityFrameworkStorageOptions>? configureStorage = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureDbContext);

        if (configureStorage is not null)
        {
            services.Configure(configureStorage);
        }

        services.AddDbContext<KrackendSecurityDbContext>(options =>
        {
            configureDbContext(options);
            options.ReplaceService<IModelCacheKeyFactory, KrackendSecurityEntityFrameworkModelCacheKeyFactory>();
        });
        services.AddScoped<IKrackendSubjectRepository, EntityFrameworkKrackendSubjectRepository>();
        services.AddScoped<IKrackendRoleAssignmentRepository, EntityFrameworkKrackendRoleAssignmentRepository>();
        services.AddScoped<IKrackendPermissionAssignmentRepository, EntityFrameworkKrackendPermissionAssignmentRepository>();
        services.AddScoped<IKrackendExternalGroupRoleAssignmentRepository, EntityFrameworkKrackendExternalGroupRoleAssignmentRepository>();
        return services;
    }
}
