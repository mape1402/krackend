using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Distribution.Entities;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Infrastructure;
using Krackend.Sagas.Orchestrations.Runtime.Distribution;
using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework;
using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Infrastructure;
using Krackend.Sagas.Orchestrations.Security.Core;
using Krackend.Sagas.Orchestrations.Security.Storage.EntityFramework;
using Microsoft.EntityFrameworkCore;
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

    private static int? GetMaxLength<TEntity>(DbContext dbContext, string propertyName)
        where TEntity : class
    {
        return dbContext.Model.FindEntityType(typeof(TEntity))?.FindProperty(propertyName)?.GetMaxLength();
    }
}
