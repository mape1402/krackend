using Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;
using Krackend.Sagas.Orchestrations.ControlPlane.Application.Distribution;
using Krackend.Sagas.Orchestrations.ControlPlane.Application.Security;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;
using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Storage;
using Krackend.Sagas.Orchestrations.ControlPlane.Security.Storage;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Infrastructure;
using Krackend.Sagas.Orchestrations.ControlPlane.WebUI;
using Krackend.Sagas.Orchestrations.WebUI.Shell.Navigation;
using Microsoft.Extensions.DependencyInjection;

namespace Krackend.Sagas.Orchestrations.Tests.ControlPlane;

public sealed class ControlPlaneCompositionTests
{
    [Fact]
    public void AddOrchestratorControlPlaneRequiresStorageConfiguration()
    {
        var services = new ServiceCollection();

        Assert.Throws<InvalidOperationException>(() =>
            services.AddOrchestratorControlPlane(_ => { }));
    }

    [Fact]
    public void AddOrchestratorControlPlaneRegistersCrossModuleServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddOrchestratorControlPlane(options =>
        {
            options.AdminRootPath = "/admin/";
            options.ConfigureStorage = _ => { };
        });

        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(OrchestratorNavigationRegistry));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(ControlPlaneDbContext));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IOrchestrationApplicationService));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IDomainRepository));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IRuntimeNodeApplicationService));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IEnvironmentRepository));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(ITeamApplicationService));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(ITeamRepository));
    }
}
