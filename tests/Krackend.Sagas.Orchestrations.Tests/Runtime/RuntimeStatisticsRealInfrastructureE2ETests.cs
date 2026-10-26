namespace Krackend.Sagas.Orchestrations.Tests.Runtime;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Runtime.Statistics;
using Krackend.Sagas.Orchestrations.Tests.Runtime.RealInfrastructure;
using Microsoft.Extensions.DependencyInjection;
using static Krackend.Sagas.Orchestrations.Tests.Runtime.RealInfrastructure.RealMessagingArtifactFactory;

[Collection(RuntimeRealInfrastructureCollection.Name)]
public sealed class RuntimeStatisticsRealInfrastructureE2ETests(RuntimeRealInfrastructureFixture infrastructure)
{
    [Fact(Timeout = 300_000)]
    public async Task CompletedSagaIsProjectedOnceAndRemainsVisibleAfterRuntimeRestart()
    {
        var suffix = Guid.NewGuid().ToString("N")[..10];
        var database = infrastructure.CreateDatabaseName();
        var version = new SemanticVersion(1, 0, 0);
        var key = $"statistics.{suffix}.sale";
        var trigger = $"statistics.{suffix}.sale.created";
        var command = $"statistics.{suffix}.sale.process";
        var correlation = $"statistics-{suffix}";
        var from = DateTime.UtcNow.Date.AddDays(-1);
        var before = DateTime.UtcNow.Date.AddDays(2);

        await using var services = await RealMessagingServiceHost.StartAsync(infrastructure,
            [new RealMessagingConsumerEndpoint(command, version.ToString())]);
        await using (var runtime = await RealMessagingRuntimeHarness.StartAsync(infrastructure, database))
        {
            var artifact = Artifact(key, version, $"statistics-{suffix}", [EventTrigger(trigger, version)],
                Stage("process", 1, MessagingTask("process", 1, version, topic: command)));
            await runtime.DeployAndWaitReadyAsync(CreatePackage(artifact));
            await runtime.PublishTriggerAsync(trigger, version.ToString(), BusinessPayload(("saleId", suffix)), correlation);
            await runtime.WaitForInstanceStatusAsync(correlation, OrchestrationInstanceStatus.Completed, TimeSpan.FromSeconds(120));

            var report = await PollingAssert.EventuallyAsync(
                async () =>
                {
                    await using var scope = runtime.Services.CreateAsyncScope();
                    return await scope.ServiceProvider.GetRequiredService<ISagaStatisticsReader>().GetReportAsync(from, before, key);
                },
                result => result.Total == 1 && result.CurrentStatuses.GetValueOrDefault(OrchestrationInstanceStatus.Completed) == 1
                    && result.PendingObservations == 0,
                "The completed saga was not projected by the scheduled Mule batch", TimeSpan.FromSeconds(120));
            Assert.Equal(1, report.ReachedStatuses[OrchestrationInstanceStatus.Completed]);
            Assert.Equal(1, report.Activity.Where(item => item.Status == OrchestrationInstanceStatus.Completed).Sum(item => item.Count));
        }

        await using var restarted = await RealMessagingRuntimeHarness.StartAsync(infrastructure, database);
        await using var restartedScope = restarted.Services.CreateAsyncScope();
        var persisted = await restartedScope.ServiceProvider.GetRequiredService<ISagaStatisticsReader>().GetReportAsync(from, before, key);
        Assert.Equal(1, persisted.Total);
        Assert.Equal(1, persisted.CurrentStatuses[OrchestrationInstanceStatus.Completed]);
        Assert.Equal(1, persisted.ReachedStatuses[OrchestrationInstanceStatus.Completed]);
        Assert.Equal(1, persisted.Activity.Where(item => item.Status == OrchestrationInstanceStatus.Completed).Sum(item => item.Count));
    }
}
