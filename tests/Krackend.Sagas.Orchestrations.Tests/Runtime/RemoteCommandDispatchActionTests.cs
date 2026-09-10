namespace Krackend.Sagas.Orchestrations.Tests.Runtime;

using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Buffering.Mule;
using Krackend.Sagas.Orchestrations.Runtime.DependencyInjection;
using Krackend.Sagas.Orchestrations.Runtime.Distribution;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Dispatching;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Mule;
using NSubstitute;

public sealed class RemoteCommandDispatchActionTests
{
    [Fact]
    public async Task ExecuteAsync_WhenTransportExecutorIsMissing_MarksActionAsTerminal()
    {
        var action = new RemoteCommandDispatchAction(
            new ServiceCollection().BuildServiceProvider(),
            Substitute.For<Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata.IOrchestrationMessageMetadataSetter>(),
            Substitute.For<IOrchestrationInstanceRepository>(),
            Substitute.For<ITaskExecutionRepository>(),
            Substitute.For<ITaskExecutionAttemptRepository>(),
            Substitute.For<ITaskDispatchRepository>(),
            Substitute.For<IExecutionTransitionRepository>());

        var command = new RemoteCommand
        {
            RemoteCommandTransport = RemoteCommandTransport.Messaging,
            Payload = "{}"
        };
        var context = new MuleActionContext<RemoteCommand>(
            new DurableAction
            {
                Key = ActionKey.From("RemoteCommandDispatch"),
                Lane = "default",
                CorrelationId = "test",
                DeduplicationKey = "test",
                Status = DurableActionStatus.Pending
            },
            new ServiceCollection().BuildServiceProvider(),
            command);

        await Assert.ThrowsAsync<RemoteCommandConfigurationException>(
            async () => await action.ExecuteAsync(context, CancellationToken.None));

        Assert.Equal(int.MaxValue - 1, context.Action.Attempts);
    }

    [Fact]
    public async Task ExecuteAsync_WhenMessagingAdapterIsMissing_MarksActionAsTerminal()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddKrackendOrchestrationsRuntime();
        var provider = services.BuildServiceProvider();

        var action = new RemoteCommandDispatchAction(
            provider,
            provider.GetRequiredService<Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata.IOrchestrationMessageMetadataSetter>(),
            Substitute.For<IOrchestrationInstanceRepository>(),
            Substitute.For<ITaskExecutionRepository>(),
            Substitute.For<ITaskExecutionAttemptRepository>(),
            Substitute.For<ITaskDispatchRepository>(),
            Substitute.For<IExecutionTransitionRepository>());

        var command = new RemoteCommand
        {
            RemoteCommandTransport = RemoteCommandTransport.Messaging,
            SettingsPayload = """{"topic":"inventory.reserve","version":"1.0.0","payload":"{}"}""",
            Payload = """{"saleId":"sale-1"}"""
        };
        var context = new MuleActionContext<RemoteCommand>(
            new DurableAction
            {
                Key = ActionKey.From("RemoteCommandDispatch"),
                Lane = "default",
                CorrelationId = "test",
                DeduplicationKey = "test",
                Status = DurableActionStatus.Pending
            },
            provider,
            command);

        await Assert.ThrowsAsync<RemoteCommandConfigurationException>(
            async () => await action.ExecuteAsync(context, CancellationToken.None));

        Assert.Equal(int.MaxValue - 1, context.Action.Attempts);
    }
}
