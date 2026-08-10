using Krackend.Sagas.Orchestrations;
using Krackend.Sagas.Orchestrations.Abstractions;
using Krackend.Sagas.Orchestrations.EntityFrameworkCore.SqlServer;
using Krackend.Sagas.Orchestrations.Messaging.Abstractions;

namespace Krackend.Sagas.Orchestrations.Tests;

public sealed class PackageSkeletonTests
{
    [Fact]
    public void MarkerTypesResolveFromExpectedAssemblies()
    {
        Assert.Equal(
            "Krackend.Sagas.Orchestrations.Abstractions",
            typeof(OrchestrationAbstractionsMarker).Assembly.GetName().Name);
        Assert.Equal(
            "Krackend.Sagas.Orchestrations.Messaging.Abstractions",
            typeof(MessagingAbstractionsMarker).Assembly.GetName().Name);
        Assert.Equal(
            "Krackend.Sagas.Orchestrations",
            typeof(OrchestrationsMarker).Assembly.GetName().Name);
        Assert.Equal(
            "Krackend.Sagas.Orchestrations.EntityFrameworkCore.SqlServer",
            typeof(OrchestrationsSqlServerMarker).Assembly.GetName().Name);
    }
}
