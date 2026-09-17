namespace Krackend.Sagas.Orchestrations.Tests.Runtime;

using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;
using Krackend.Sagas.Orchestrations.Runtime.DependencyInjection;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Dispatching;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;

public sealed class RemoteCommandDispatcherTests
{
    [Fact]
    public async Task DispatchAsync_UsesTransportExecutorAndSetsMessageMetadata()
    {
        var executor = Substitute.For<IRemoteCommandExecutor>();
        var metadataSetter = Substitute.For<IOrchestrationMessageMetadataSetter>();
        var metadata = new OrchestrationMessageMetadata
        {
            OrchestrationInstanceId = "instance-1",
            TaskExecutionId = "task-1",
            CorrelationId = "correlation-1"
        };
        var command = new RemoteCommand
        {
            RemoteCommandTransport = RemoteCommandTransport.Messaging,
            Payload = """{"saleId":"sale-1"}""",
            MessageMetadata = metadata
        };
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddKrackendOrchestrationsRuntime();
        services.Replace(ServiceDescriptor.Scoped(_ => metadataSetter));
        services.AddKeyedScoped(RemoteCommandTransport.Messaging, (_, _) => executor);
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IRemoteCommandDispatcher>();

        await dispatcher.DispatchAsync(command);

        metadataSetter.Received(1).Set(metadata);
        await executor.Received(1).ExecuteAsync(command, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DispatchAsync_WhenMetadataIsMissing_SetsEmptyMetadataBeforeExecuting()
    {
        var executor = Substitute.For<IRemoteCommandExecutor>();
        var metadataSetter = Substitute.For<IOrchestrationMessageMetadataSetter>();
        var command = new RemoteCommand
        {
            RemoteCommandTransport = RemoteCommandTransport.Messaging,
            Payload = "{}"
        };
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddKrackendOrchestrationsRuntime();
        services.Replace(ServiceDescriptor.Scoped(_ => metadataSetter));
        services.AddKeyedScoped(RemoteCommandTransport.Messaging, (_, _) => executor);
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IRemoteCommandDispatcher>();

        await dispatcher.DispatchAsync(command);

        metadataSetter.Received(1).Set(Arg.Is<OrchestrationMessageMetadata>(metadata =>
            string.IsNullOrWhiteSpace(metadata.OrchestrationInstanceId) &&
            string.IsNullOrWhiteSpace(metadata.TaskExecutionId)));
        await executor.Received(1).ExecuteAsync(command, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DispatchAsync_WhenTransportExecutorIsMissing_ThrowsConfigurationException()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddKrackendOrchestrationsRuntime();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IRemoteCommandDispatcher>();

        var exception = await Assert.ThrowsAsync<RemoteCommandConfigurationException>(() =>
            dispatcher.DispatchAsync(new RemoteCommand
            {
                RemoteCommandTransport = RemoteCommandTransport.Http,
                Payload = "{}"
            }));

        Assert.Contains("No remote command executor is configured", exception.Message);
    }
}
