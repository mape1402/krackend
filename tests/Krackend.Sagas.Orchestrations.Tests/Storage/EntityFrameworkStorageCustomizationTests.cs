using Krackend.Sagas.Orchestrations.ControlPlane.Application.Distribution;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Distribution.Entities;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Infrastructure;
using Krackend.Sagas.Orchestrations.Runtime.DependencyInjection;
using Krackend.Sagas.Orchestrations.Runtime.Distribution;
using Krackend.Sagas.Orchestrations.Runtime.Extensions;
using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework;
using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Infrastructure;
using Krackend.Sagas.Orchestrations.Security.Core;
using Krackend.Sagas.Orchestrations.Security.Storage.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace Krackend.Sagas.Orchestrations.Tests.Storage;

public sealed class EntityFrameworkStorageCustomizationTests
{
    [Fact]
    public void RuntimeStorageAllowsHostModelCustomization()
    {
        var services = new ServiceCollection();
        services.AddOrchestratorRuntimeStorageEntityFramework(
            options => options.UseInMemoryDatabase(Guid.NewGuid().ToString("N")),
            storage => storage.ConfigureModel = modelBuilder =>
                modelBuilder.Entity<RuntimeDesignNode>().Property(x => x.Name).HasMaxLength(512));

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RuntimeDbContext>();

        Assert.Equal(512, GetMaxLength<RuntimeDesignNode>(dbContext, nameof(RuntimeDesignNode.Name)));
    }

#if NET10_0
    [Fact]
    public void RuntimeStorageModelIncludesExtensionPackagesWhenConfiguredForMongo()
    {
        var services = new ServiceCollection();
        services.AddOrchestratorRuntimeStorageEntityFramework(
            options => options.UseMongoDB(
                "mongodb://localhost:27017",
                $"krackend-runtime-model-{Guid.NewGuid():N}"));

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RuntimeDbContext>();

        var entityType = dbContext.Model.FindEntityType(typeof(RuntimeExtensionPackage));

        Assert.NotNull(entityType);
        Assert.Equal("RuntimeExtensionPackages", entityType!.GetTableName());
        Assert.NotNull(entityType.FindProperty(nameof(RuntimeExtensionPackage.Manifest))?.GetValueConverter());
        Assert.NotNull(entityType.FindProperty(nameof(RuntimeExtensionPackage.Version))?.GetValueConverter());
    }
#endif

    [Fact]
    public void ControlPlaneStorageAllowsHostModelCustomization()
    {
        var services = new ServiceCollection();
        services.AddOrchestratorControlPlaneStorageEntityFramework(
            options => options.UseInMemoryDatabase(Guid.NewGuid().ToString("N")),
            storage => storage.ConfigureModel = modelBuilder =>
                modelBuilder.Entity<RuntimeNodeEntity>().Property(x => x.Name).HasMaxLength(512));

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ControlPlaneDbContext>();

        Assert.Equal(512, GetMaxLength<RuntimeNodeEntity>(dbContext, nameof(RuntimeNodeEntity.Name)));
    }

    [Fact]
    public void SecurityStorageAllowsHostModelCustomization()
    {
        var services = new ServiceCollection();
        services.AddKrackendSecurityStorageEntityFramework(
            options => options.UseInMemoryDatabase(Guid.NewGuid().ToString("N")),
            storage => storage.ConfigureModel = modelBuilder =>
                modelBuilder.Entity<KrackendSubject>().Property(x => x.DisplayName).HasMaxLength(512));

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<KrackendSecurityDbContext>();

        Assert.Equal(512, GetMaxLength<KrackendSubject>(dbContext, nameof(KrackendSubject.DisplayName)));
    }

    [Fact]
    public void ControlPlaneStoragePersistsDistributionSecretProtectionKeysInDatabaseByDefault()
    {
        var databaseRoot = new InMemoryDatabaseRoot();
        var databaseName = $"control-plane-dp-{Guid.NewGuid():N}";
        string protectedSecret;

        using (var provider = CreateControlPlaneDistributionProvider(databaseName, databaseRoot))
        {
            protectedSecret = provider
                .GetRequiredService<IControlPlaneRuntimeNodeSecretProtector>()
                .Protect("runtime-secret");
        }

        using (var provider = CreateControlPlaneDistributionProvider(databaseName, databaseRoot))
        {
            var secret = provider
                .GetRequiredService<IControlPlaneRuntimeNodeSecretProtector>()
                .Unprotect(protectedSecret);

            Assert.Equal("runtime-secret", secret);
        }
    }

    [Fact]
    public void RuntimeStoragePersistsDistributionSecretProtectionKeysInDatabaseByDefault()
    {
        var databaseRoot = new InMemoryDatabaseRoot();
        var databaseName = $"runtime-dp-{Guid.NewGuid():N}";
        string protectedSecret;

        using (var provider = CreateRuntimeDistributionProvider(databaseName, databaseRoot))
        {
            protectedSecret = provider
                .GetRequiredService<IRuntimeDesignNodeSecretProtector>()
                .Protect("design-secret");
        }

        using (var provider = CreateRuntimeDistributionProvider(databaseName, databaseRoot))
        {
            var secret = provider
                .GetRequiredService<IRuntimeDesignNodeSecretProtector>()
                .Unprotect(protectedSecret);

            Assert.Equal("design-secret", secret);
        }
    }

    private static int? GetMaxLength<TEntity>(DbContext dbContext, string propertyName)
        where TEntity : class
    {
        return dbContext.Model.FindEntityType(typeof(TEntity))?.FindProperty(propertyName)?.GetMaxLength();
    }

    private static ServiceProvider CreateControlPlaneDistributionProvider(
        string databaseName,
        InMemoryDatabaseRoot databaseRoot)
    {
        var services = new ServiceCollection();
        services.AddOrchestratorDistributionApplication();
        services.AddOrchestratorControlPlaneStorageEntityFramework(
            options => options.UseInMemoryDatabase(databaseName, databaseRoot));
        return services.BuildServiceProvider();
    }

    private static ServiceProvider CreateRuntimeDistributionProvider(
        string databaseName,
        InMemoryDatabaseRoot databaseRoot)
    {
        var services = new ServiceCollection();
        services.AddKrackendOrchestrationsRuntime();
        services.AddOrchestratorRuntimeStorageEntityFramework(
            options => options.UseInMemoryDatabase(databaseName, databaseRoot));
        return services.BuildServiceProvider();
    }
}
