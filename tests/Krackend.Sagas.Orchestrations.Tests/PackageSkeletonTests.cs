using Krackend.Sagas.Orchestrations.Abstractions;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Infrastructure;
using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Infrastructure;
using Krackend.Sagas.Orchestrations.Runtime.WebUI.Diagnostics;

namespace Krackend.Sagas.Orchestrations.Tests;

public sealed class PackageSkeletonTests
{
    [Fact]
    public void RepresentativeTypesResolveFromExpectedAssemblies()
    {
        Assert.Equal(
            "Krackend.Sagas.Orchestrations.Abstractions",
            typeof(OrchestrationAbstractionsMarker).Assembly.GetName().Name);
        Assert.Equal(
            "Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework",
            typeof(ControlPlaneDbContext).Assembly.GetName().Name);
        Assert.Equal(
            "Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework",
            typeof(RuntimeDbContext).Assembly.GetName().Name);
        Assert.Equal(
            "Krackend.Sagas.Orchestrations.Runtime.WebUI",
            typeof(RuntimeDiagnosticsReader).Assembly.GetName().Name);
    }
}
