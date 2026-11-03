using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Diagnostics;
using NSubstitute;

namespace Krackend.Sagas.Orchestrations.Tests.WebUI;

public sealed class RuntimeInstanceCatalogReaderTests
{
    [Fact]
    public async Task ReportsUnsupportedStorageWithoutLoadingAnInMemorySnapshot()
    {
        var instances = Substitute.For<IOrchestrationInstanceRepository>();
        var reader = CreateReader(instances, Substitute.For<IRuntimeArtifactRepository>());

        await Assert.ThrowsAsync<NotSupportedException>(() => reader.GetInstances(new RuntimeInstanceCatalogQuery()));
        await instances.DidNotReceiveWithAnyArgs().GetRecent();
    }

    [Fact]
    public async Task GetStatusCounts_ReportsUnsupportedStorageWithoutQueryingInstances()
    {
        var instances = Substitute.For<IOrchestrationInstanceRepository>();
        var reader = CreateReader(instances, Substitute.For<IRuntimeArtifactRepository>());

        await Assert.ThrowsAsync<NotSupportedException>(() => reader.GetStatusCounts(new RuntimeInstanceCatalogQuery()));
        await instances.DidNotReceiveWithAnyArgs().GetRecent();
    }

    [Fact]
    public async Task MapsProjectedPageWithoutLoadingArtifacts()
    {
        var instances = Substitute.For<IOrchestrationInstanceRepository, IRuntimeInstanceCatalogRepository>();
        var catalog = (IRuntimeInstanceCatalogRepository)instances;
        var artifactId = Id.New();
        var entry = new OrchestrationInstance
        {
            Id = Id.New(), RuntimeOrchestrationArtifactId = artifactId, TriggerIntakeId = Id.New(),
            OrchestrationDefinitionKey = "sales", CorrelationId = "corr", SagaId = "saga", ExecutionKey = "execution",
            OrchestrationVersion = "1.2.3", TotalTaskCount = 4, CompletedTaskCount = 2
        };
        catalog.GetPage(Arg.Any<RuntimeInstanceCatalogQuery>(), Arg.Any<CancellationToken>())
            .Returns(new RuntimeInstanceCatalogPage([entry], 30, 2, 25));
        var artifacts = Substitute.For<IRuntimeArtifactRepository>();
        artifacts.GetById(artifactId, Arg.Any<CancellationToken>()).Returns<Task<RuntimeOrchestrationArtifact>>(_ => throw new KeyNotFoundException());
        var reader = CreateReader(instances, artifacts);

        var result = await reader.GetInstances(new RuntimeInstanceCatalogQuery(2));

        Assert.Equal(30, result.TotalCount);
        var row = Assert.Single(result.Items);
        Assert.Equal("sales", row.OrchestrationDefinitionKey);
        Assert.Equal("1.2.3", row.OrchestrationVersion);
        Assert.Equal(4, row.TotalTaskCount);
        Assert.Equal(2, row.CompletedTaskCount);
        await artifacts.DidNotReceiveWithAnyArgs().GetById(default);
        await artifacts.DidNotReceiveWithAnyArgs().GetAll();
    }

    private static RuntimeDiagnosticsReader CreateReader(IOrchestrationInstanceRepository instances, IRuntimeArtifactRepository artifacts)
        => new(artifacts, instances, Substitute.For<IStageExecutionRepository>(), Substitute.For<ITaskExecutionRepository>(),
            Substitute.For<ITaskExecutionAttemptRepository>(), Substitute.For<ITaskDispatchRepository>(),
            Substitute.For<ICompensationExecutionRepository>(), Substitute.For<IExecutionTransitionRepository>());
}
