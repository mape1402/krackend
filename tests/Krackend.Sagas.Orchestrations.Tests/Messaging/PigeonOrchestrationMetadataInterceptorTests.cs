namespace Krackend.Sagas.Orchestrations.Tests.Messaging;

using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;
using NSubstitute;
using Pigeon.Messaging.Consuming.Dispatching;
using Pigeon.Messaging.Producing;
using System.Collections.Concurrent;
using System.Reflection;

public sealed class PigeonOrchestrationMetadataInterceptorTests
{
    private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    [Theory]
    [InlineData("Krackend.Sagas.Orchestrations.Runtime.Messaging.Pigeon", "Krackend.Sagas.Orchestrations.Runtime.Messaging.Pigeon.Interceptors.KrackendPublishInterceptor")]
    [InlineData("Krackend.Sagas.Orchestrations.Client.Messaging.Pigeon", "Krackend.Sagas.Orchestrations.Client.Messaging.Pigeon.KrackendClientPublishInterceptor")]
    public async Task PublishInterceptorsAttachOrchestrationMetadataWithoutChangingPayload(string assemblyName, string typeName)
    {
        var messageMetadata = new OrchestrationMessageMetadata
        {
            OrchestrationInstanceId = "instance-1",
            TaskExecutionId = "task-1",
            CorrelationId = "correlation-1"
        };
        var resultMetadata = new OrchestrationExecutionResultMetadata
        {
            Succeeded = true,
            Status = "Succeeded",
            ServiceName = "inventories"
        };
        var messageAccessor = Substitute.For<IOrchestrationMessageMetadataAccessor>();
        var resultAccessor = Substitute.For<IOrchestrationExecutionResultMetadataAccessor>();
        messageAccessor.Get().Returns(messageMetadata);
        resultAccessor.Get().Returns(resultMetadata);
        var interceptor = CreatePublishInterceptor(assemblyName, typeName, messageAccessor, resultAccessor);
        var context = new PublishContext();

        await interceptor.Intercept(context, CancellationToken.None);

        var metadata = GetPublishMetadata(context);
        Assert.Same(messageMetadata, metadata[OrchestrationMetadataConstants.OrchestrationMessageMetadataKey]);
        Assert.Same(resultMetadata, metadata[OrchestrationMetadataConstants.OrchestrationExecutionResultMetadataKey]);
    }

    [Theory]
    [InlineData("Krackend.Sagas.Orchestrations.Runtime.Messaging.Pigeon", "Krackend.Sagas.Orchestrations.Runtime.Messaging.Pigeon.Interceptors.KrackendPublishInterceptor")]
    [InlineData("Krackend.Sagas.Orchestrations.Client.Messaging.Pigeon", "Krackend.Sagas.Orchestrations.Client.Messaging.Pigeon.KrackendClientPublishInterceptor")]
    public async Task PublishInterceptorsSkipEmptyMessageMetadataButKeepExecutionResult(string assemblyName, string typeName)
    {
        var resultMetadata = new OrchestrationExecutionResultMetadata
        {
            Succeeded = false,
            Status = "Failed",
            ErrorCode = "InventoryUnavailable"
        };
        var messageAccessor = Substitute.For<IOrchestrationMessageMetadataAccessor>();
        var resultAccessor = Substitute.For<IOrchestrationExecutionResultMetadataAccessor>();
        messageAccessor.Get().Returns(new OrchestrationMessageMetadata());
        resultAccessor.Get().Returns(resultMetadata);
        var interceptor = CreatePublishInterceptor(assemblyName, typeName, messageAccessor, resultAccessor);
        var context = new PublishContext();

        await interceptor.Intercept(context, CancellationToken.None);

        var metadata = GetPublishMetadata(context);
        Assert.False(metadata.ContainsKey(OrchestrationMetadataConstants.OrchestrationMessageMetadataKey));
        Assert.Same(resultMetadata, metadata[OrchestrationMetadataConstants.OrchestrationExecutionResultMetadataKey]);
    }

    [Fact]
    public async Task RuntimeConsumeInterceptorReadsMetadataAndClearsMissingExecutionResult()
    {
        var messageMetadata = new OrchestrationMessageMetadata
        {
            OrchestrationInstanceId = "instance-1",
            TaskExecutionId = "task-1"
        };
        var resultMetadata = new OrchestrationExecutionResultMetadata
        {
            Succeeded = true,
            Status = "Succeeded"
        };
        var messageSetter = Substitute.For<IOrchestrationMessageMetadataSetter>();
        var resultSetter = Substitute.For<IOrchestrationExecutionResultMetadataSetter>();
        var interceptor = CreateConsumeInterceptor(messageSetter, resultSetter);
        var context = new ConsumeContext();
        SetConsumeMetadata(context, messageMetadata, resultMetadata);

        await interceptor.Intercept(context, CancellationToken.None);

        messageSetter.Received(1).Set(messageMetadata);
        resultSetter.Received(1).Clear();
        resultSetter.Received(1).Set(resultMetadata);

        messageSetter.ClearReceivedCalls();
        resultSetter.ClearReceivedCalls();
        await interceptor.Intercept(new ConsumeContext(), CancellationToken.None);

        messageSetter.Received(1).Set(Arg.Is<OrchestrationMessageMetadata>(metadata =>
            string.IsNullOrWhiteSpace(metadata.OrchestrationInstanceId) &&
            string.IsNullOrWhiteSpace(metadata.TaskExecutionId)));
        resultSetter.Received(2).Clear();
        resultSetter.DidNotReceive().Set(Arg.Any<OrchestrationExecutionResultMetadata>());
    }

    [Fact]
    public async Task ClientConsumeInterceptorReadsMessageMetadataAndClearsExecutionResult()
    {
        var messageMetadata = new OrchestrationMessageMetadata
        {
            OrchestrationInstanceId = "instance-1",
            TaskExecutionId = "task-1",
            CorrelationId = "correlation-1"
        };
        var resultMetadata = new OrchestrationExecutionResultMetadata
        {
            Succeeded = true,
            Status = "Succeeded"
        };
        var messageSetter = Substitute.For<IOrchestrationMessageMetadataSetter>();
        var resultSetter = Substitute.For<IOrchestrationExecutionResultMetadataSetter>();
        var interceptor = CreateClientConsumeInterceptor(messageSetter, resultSetter);
        var context = new ConsumeContext();
        SetConsumeMetadata(context, messageMetadata, resultMetadata);

        await interceptor.Intercept(context, CancellationToken.None);

        messageSetter.Received(1).Set(messageMetadata);
        resultSetter.Received(1).Clear();
        resultSetter.DidNotReceive().Set(Arg.Any<OrchestrationExecutionResultMetadata>());

        messageSetter.ClearReceivedCalls();
        resultSetter.ClearReceivedCalls();
        await interceptor.Intercept(new ConsumeContext(), CancellationToken.None);

        messageSetter.Received(1).Set(Arg.Is<OrchestrationMessageMetadata>(metadata =>
            string.IsNullOrWhiteSpace(metadata.OrchestrationInstanceId) &&
            string.IsNullOrWhiteSpace(metadata.TaskExecutionId)));
        resultSetter.Received(1).Clear();
    }

    private static Pigeon.Messaging.Producing.IPublishInterceptor CreatePublishInterceptor(
        string assemblyName,
        string typeName,
        IOrchestrationMessageMetadataAccessor messageAccessor,
        IOrchestrationExecutionResultMetadataAccessor resultAccessor)
    {
        var assembly = Assembly.Load(assemblyName);
        var type = assembly.GetType(typeName, throwOnError: true)!;
        return (Pigeon.Messaging.Producing.IPublishInterceptor)Activator.CreateInstance(
            type,
            InstanceFlags,
            binder: null,
            args: [messageAccessor, resultAccessor],
            culture: null)!;
    }

    private static Pigeon.Messaging.Consuming.Dispatching.IConsumeInterceptor CreateConsumeInterceptor(
        IOrchestrationMessageMetadataSetter messageSetter,
        IOrchestrationExecutionResultMetadataSetter resultSetter)
    {
        var assembly = Assembly.Load("Krackend.Sagas.Orchestrations.Runtime.Messaging.Pigeon");
        var type = assembly.GetType(
            "Krackend.Sagas.Orchestrations.Runtime.Messaging.Pigeon.Interceptors.KrackendConsumeInterceptor",
            throwOnError: true)!;
        return (Pigeon.Messaging.Consuming.Dispatching.IConsumeInterceptor)Activator.CreateInstance(
            type,
            InstanceFlags,
            binder: null,
            args: [messageSetter, resultSetter],
            culture: null)!;
    }

    private static Pigeon.Messaging.Consuming.Dispatching.IConsumeInterceptor CreateClientConsumeInterceptor(
        IOrchestrationMessageMetadataSetter messageSetter,
        IOrchestrationExecutionResultMetadataSetter resultSetter)
    {
        var assembly = Assembly.Load("Krackend.Sagas.Orchestrations.Client.Messaging.Pigeon");
        var type = assembly.GetType(
            "Krackend.Sagas.Orchestrations.Client.Messaging.Pigeon.KrackendClientConsumeInterceptor",
            throwOnError: true)!;
        return (Pigeon.Messaging.Consuming.Dispatching.IConsumeInterceptor)Activator.CreateInstance(
            type,
            InstanceFlags,
            binder: null,
            args: [messageSetter, resultSetter],
            culture: null)!;
    }

    private static IReadOnlyDictionary<string, object> GetPublishMetadata(PublishContext context)
    {
        var method = typeof(PublishContext).GetMethod("GetMetadata", InstanceFlags)!;
        return (IReadOnlyDictionary<string, object>)method.Invoke(context, [])!;
    }

    private static void SetConsumeMetadata(
        ConsumeContext context,
        OrchestrationMessageMetadata messageMetadata,
        OrchestrationExecutionResultMetadata resultMetadata)
    {
        var field = typeof(ConsumeContext).GetField("_metadata", InstanceFlags)!;
        var metadata = (ConcurrentDictionary<string, object>)field.GetValue(context)!;
        metadata[OrchestrationMetadataConstants.OrchestrationMessageMetadataKey] = messageMetadata;
        metadata[OrchestrationMetadataConstants.OrchestrationExecutionResultMetadataKey] = resultMetadata;
    }
}
