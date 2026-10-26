namespace Krackend.Sagas.Orchestrations.Tests.Runtime;

using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Reactive;
using Krackend.Sagas.Orchestrations.Runtime.Buffering.Mule;
using Krackend.Sagas.Orchestrations.Runtime.Statistics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

public sealed class SagaStatisticsBatchActionTests
{
    [Fact]
    public async Task MissingOptionalServicesAreIgnored()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var action = new SagaStatisticsBatchAction(services, Options.Create(new SagaStatisticsMuleOptions()), NullLogger<SagaStatisticsBatchAction>.Instance);
        await action.ExecuteAsync(null!, CancellationToken.None);
    }

    [Theory]
    [InlineData(0, 0, false)]
    [InlineData(1, 1, false)]
    [InlineData(1, 1, true)]
    public async Task OnlyAppliedBatchesNotifyAndNotificationFailuresDoNotFailBatch(int applied, int expected, bool fail)
    {
        var backfill = Substitute.For<ISagaStatisticsBackfill>();
        var processor = Substitute.For<ISagaStatisticsBatchProcessor>();
        var local = Substitute.For<IRuntimeStatisticsUpdatePublisher>();
        var fanout = Substitute.For<IRuntimeStatisticsFanout>();
        backfill.ProcessAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(true);
        processor.ProcessAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(new SagaStatisticsBatchResult(1, applied, 0));
        local.PublishAsync(Arg.Any<CancellationToken>()).Returns(_ => fail ? Task.FromException(new InvalidOperationException()) : Task.CompletedTask);
        fanout.PublishStatisticsUpdatedAsync(Arg.Any<CancellationToken>()).Returns(_ => fail ? Task.FromException(new InvalidOperationException()) : Task.CompletedTask);
        using var services = new ServiceCollection().AddSingleton(backfill).AddSingleton(processor).AddSingleton(local).AddSingleton(fanout).BuildServiceProvider();
        var action = new SagaStatisticsBatchAction(services, Options.Create(new SagaStatisticsMuleOptions { BatchSize = 5000 }), NullLogger<SagaStatisticsBatchAction>.Instance);

        await action.ExecuteAsync(null!, CancellationToken.None);

        await backfill.Received(1).ProcessAsync(1000, Arg.Any<CancellationToken>());
        await processor.Received(1).ProcessAsync(1000, Arg.Any<CancellationToken>());
        await local.Received(expected).PublishAsync(Arg.Any<CancellationToken>());
        await fanout.Received(expected).PublishStatisticsUpdatedAsync(Arg.Any<CancellationToken>());
    }
}
