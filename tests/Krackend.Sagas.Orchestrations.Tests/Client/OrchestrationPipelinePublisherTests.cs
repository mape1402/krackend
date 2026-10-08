namespace Krackend.Sagas.Orchestrations.Tests.Client;

using System.Reflection;
using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;
using Krackend.Sagas.Orchestrations.Client.DependencyInjection;
using Krackend.Sagas.Orchestrations.Client.Publishing;
using Krackend.Sagas.Orchestrations.Tests.Client.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;

public sealed class OrchestrationPipelinePublisherTests
{
    [Fact]
    public async Task DefaultClientPublisherFailsExplicitlyWhenNoTransportPublisherIsConfigured()
    {
        var publisherType = typeof(OrchestrationOperationOptions).Assembly.GetType(
            "Krackend.Sagas.Orchestrations.Client.Publishing.DefaultOrchestrationClientPublisher",
            throwOnError: true)!;
        var publisher = (IOrchestrationClientPublisher)Activator.CreateInstance(publisherType)!;

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            publisher.PublishAsync(new { ok = true }, new OrchestrationReplyAddress(), CancellationToken.None));

        Assert.Contains("No orchestration client publisher", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ClientBuilderRegistersAccessorAndRejectsInvalidConfiguration()
    {
        var services = new ServiceCollection();
        var builder = new KrackendOrchestrationsClientBuilder(services);

        var returned = builder
            .UseTriggerMetadataAccessor<StaticTriggerMetadataAccessor>()
            .MapException<InvalidOperationException>("InvalidOperation", true)
            .MapException<ArgumentException>("ArgumentError", exception => exception.ParamName == "value", false);

        Assert.Same(builder, returned);
        Assert.Same(services, builder.Services);
        Assert.Throws<ArgumentNullException>(() => new KrackendOrchestrationsClientBuilder(null!));
        Assert.Throws<ArgumentNullException>(() => builder.ConfigureErrorMapping(null!));
        using var provider = services.BuildServiceProvider();
        Assert.IsType<StaticTriggerMetadataAccessor>(
            provider.GetRequiredService<IOrchestrationTriggerMetadataAccessor>());
    }

    [Fact]
    public async Task PublishSuccessAsync_WithTriggerDestination_PublishesTriggerMetadataAndRestoresPreviousScope()
    {
        var triggerAccessor = Substitute.For<IOrchestrationTriggerMetadataAccessor>();
        triggerAccessor.GetAsync(Arg.Any<CancellationToken>())
            .Returns(new ValueTask<OrchestrationTriggerMetadata>(new OrchestrationTriggerMetadata
            {
                CorrelationId = "trigger-correlation",
                EventId = "event-1",
                EventType = "order.created"
            }));
        using var scope = CreateScope(triggerAccessor);
        var messageSetter = scope.ServiceProvider.GetRequiredService<IOrchestrationMessageMetadataSetter>();
        var messageAccessor = scope.ServiceProvider.GetRequiredService<IOrchestrationMessageMetadataAccessor>();
        var propagationSetter = scope.ServiceProvider.GetRequiredService<IOrchestrationPropagationMetadataSetter>();
        var propagationAccessor = scope.ServiceProvider.GetRequiredService<IOrchestrationPropagationMetadataAccessor>();
        var previousMessage = new OrchestrationMessageMetadata
        {
            SagaId = "saga-1",
            OrchestrationInstanceId = "instance-1",
            CurrentStage = "stage-1",
            CurrentTasks = ["task-1"],
            CorrelationId = "previous-correlation",
            TaskExecutionId = "task-execution-1",
            DispatchId = "dispatch-1",
            Attempt = 2
        };
        var previousPropagation = new OrchestrationPropagationMetadata
        {
            Items =
            {
                ["existing"] = JsonValue.Create("kept")
            }
        };
        messageSetter.Set(previousMessage);
        propagationSetter.Set(previousPropagation);

        var pipeline = GetPipelinePublisher(scope.ServiceProvider);
        var triggerAddress = new OrchestrationReplyAddress
        {
            Transport = OrchestrationTransportNames.Messaging,
            SettingsPayload = "trigger-address"
        };

        await InvokePublishSuccessAsync(
            pipeline,
            typeof(string),
            typeof(int),
            new { ok = true },
            new OrchestrationOperationOptions { TriggerAddress = triggerAddress });

        var publisher = (RecordingOrchestrationClientPublisher)scope.ServiceProvider.GetRequiredService<IOrchestrationClientPublisher>();
        var message = Assert.Single(publisher.Messages);
        Assert.Same(triggerAddress, message.Address);
        Assert.Equal("trigger-correlation", message.MessageMetadata!.CorrelationId);
        Assert.Null(message.MessageMetadata.SagaId);
        Assert.Null(message.MessageMetadata.OrchestrationInstanceId);
        Assert.Null(message.MessageMetadata.ReplyAddress);
        Assert.Equal(0, message.MessageMetadata.Attempt);
        Assert.Equal("kept", message.PropagationMetadata!.Items["existing"]!.GetValue<string>());
        Assert.Equal("event-1", message.PropagationMetadata.Items[OrchestrationMetadataConstants.TriggerMetadataKey]!["EventId"]!.GetValue<string>());
        var origin = message.PropagationMetadata.Items[OrchestrationMetadataConstants.OriginMetadataKey]!;
        Assert.Equal("saga-1", origin[nameof(OrchestrationOriginMetadata.SagaId)]!.GetValue<string>());
        Assert.Equal("instance-1", origin[nameof(OrchestrationOriginMetadata.OrchestrationInstanceId)]!.GetValue<string>());
        Assert.Equal("stage-1", origin[nameof(OrchestrationOriginMetadata.StageKey)]!.GetValue<string>());
        Assert.Equal("task-1", origin[nameof(OrchestrationOriginMetadata.TaskKeys)]![0]!.GetValue<string>());
        Assert.Equal(2, origin[nameof(OrchestrationOriginMetadata.Attempt)]!.GetValue<int>());
        Assert.Equal("previous-correlation", messageAccessor.Get().CorrelationId);
        Assert.Equal("kept", propagationAccessor.Get().Items["existing"]!.GetValue<string>());
    }

    [Fact]
    public async Task PublishSuccessAsync_UsesPreviousCorrelationWhenTriggerMetadataIsBlank()
    {
        var triggerAccessor = Substitute.For<IOrchestrationTriggerMetadataAccessor>();
        triggerAccessor.GetAsync(Arg.Any<CancellationToken>())
            .Returns(new ValueTask<OrchestrationTriggerMetadata>((OrchestrationTriggerMetadata)null!));
        using var scope = CreateScope(triggerAccessor);
        scope.ServiceProvider.GetRequiredService<IOrchestrationMessageMetadataSetter>().Set(new OrchestrationMessageMetadata
        {
            CorrelationId = "previous-correlation"
        });

        await InvokePublishSuccessAsync(
            GetPipelinePublisher(scope.ServiceProvider),
            typeof(string),
            typeof(int),
            new { ok = true },
            new OrchestrationOperationOptions
            {
                TriggerAddress = new OrchestrationReplyAddress
                {
                    Transport = OrchestrationTransportNames.Messaging,
                    SettingsPayload = "trigger-address"
                }
            });

        var publisher = (RecordingOrchestrationClientPublisher)scope.ServiceProvider.GetRequiredService<IOrchestrationClientPublisher>();
        var message = Assert.Single(publisher.Messages);
        Assert.Equal("previous-correlation", message.MessageMetadata!.CorrelationId);
        Assert.True(message.PropagationMetadata!.Items.ContainsKey(OrchestrationMetadataConstants.TriggerMetadataKey));
    }

    [Fact]
    public async Task PublishFailureAsync_SkipsPublishWhenReplyAddressIsMissingOrIncomplete()
    {
        using var scope = CreateScope();
        var messageSetter = scope.ServiceProvider.GetRequiredService<IOrchestrationMessageMetadataSetter>();
        var pipeline = GetPipelinePublisher(scope.ServiceProvider);

        await InvokePublishFailureAsync(pipeline, typeof(string), new InvalidOperationException("failed"), new OrchestrationOperationOptions());

        messageSetter.Set(new OrchestrationMessageMetadata
        {
            ReplyAddress = new OrchestrationReplyAddress()
        });
        await InvokePublishFailureAsync(pipeline, typeof(string), new InvalidOperationException("failed"), new OrchestrationOperationOptions());

        messageSetter.Set(new OrchestrationMessageMetadata
        {
            ReplyAddress = new OrchestrationReplyAddress
            {
                Transport = OrchestrationTransportNames.Messaging,
                SettingsPayload = " "
            }
        });
        await InvokePublishFailureAsync(pipeline, typeof(string), new InvalidOperationException("failed"), new OrchestrationOperationOptions());

        var publisher = (RecordingOrchestrationClientPublisher)scope.ServiceProvider.GetRequiredService<IOrchestrationClientPublisher>();
        Assert.Equal(0, publisher.PublishCount);
    }

    [Fact]
    public void PrivateHelpersCoverNullMetadataAndConstructorGuardBranches()
    {
        using var scope = CreateScope();
        var pipeline = GetPipelinePublisher(scope.ServiceProvider);

        Assert.False(InvokePipelinePrivateInstance<bool>(
            pipeline,
            "HasReplyAddress",
            [typeof(OrchestrationMessageMetadata)],
            [null]));
        AssertConstructorGuards(scope.ServiceProvider);
    }

    private static IServiceScope CreateScope(IOrchestrationTriggerMetadataAccessor? triggerAccessor = null)
    {
        var services = new ServiceCollection();
        services.AddKrackendOrchestrationsClient();
        services.Replace(ServiceDescriptor.Scoped<IOrchestrationClientPublisher, RecordingOrchestrationClientPublisher>());
        if (triggerAccessor is not null)
        {
            services.Replace(ServiceDescriptor.Scoped(_ => triggerAccessor));
        }

        return services.BuildServiceProvider().CreateScope();
    }

    private static object GetPipelinePublisher(IServiceProvider provider)
    {
        var pipelineType = typeof(OrchestrationOperationOptions).Assembly.GetType(
            "Krackend.Sagas.Orchestrations.Client.Publishing.IOrchestrationPipelinePublisher")!;
        return provider.GetRequiredService(pipelineType);
    }

    private static Task InvokePublishSuccessAsync(
        object pipeline,
        Type requestType,
        Type responseType,
        object payload,
        OrchestrationOperationOptions options)
        => (Task)pipeline.GetType()
            .GetMethod(
                "PublishSuccessAsync",
                [typeof(Type), typeof(Type), typeof(object), typeof(OrchestrationOperationOptions), typeof(CancellationToken)])!
            .Invoke(pipeline, [requestType, responseType, payload, options, CancellationToken.None])!;

    private static Task InvokePublishFailureAsync(
        object pipeline,
        Type requestType,
        Exception exception,
        OrchestrationOperationOptions options)
        => (Task)pipeline.GetType()
            .GetMethod(
                "PublishFailureAsync",
                [typeof(Type), typeof(Exception), typeof(OrchestrationOperationOptions), typeof(CancellationToken)])!
            .Invoke(pipeline, [requestType, exception, options, CancellationToken.None])!;

    private static void AssertConstructorGuards(IServiceProvider provider)
    {
        var publisherType = typeof(OrchestrationOperationOptions).Assembly.GetType(
            "Krackend.Sagas.Orchestrations.Client.Publishing.DefaultOrchestrationPipelinePublisher")!;
        var constructor = publisherType.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Single();
        var arguments = constructor.GetParameters()
            .Select(parameter => provider.GetRequiredService(parameter.ParameterType))
            .ToArray();

        for (var index = 0; index < arguments.Length; index++)
        {
            var candidate = arguments.ToArray();
            candidate[index] = null!;

            var exception = Assert.Throws<TargetInvocationException>(() => constructor.Invoke(candidate));
            Assert.IsType<ArgumentNullException>(exception.InnerException);
        }
    }

    private static TResult InvokePipelinePrivateInstance<TResult>(
        object instance,
        string methodName,
        Type[] parameterTypes,
        object?[] arguments)
    {
        var method = instance.GetType().GetMethod(
            methodName,
            BindingFlags.NonPublic | BindingFlags.Instance,
            binder: null,
            types: parameterTypes,
            modifiers: null)!;
        return (TResult)method.Invoke(instance, arguments)!;
    }

    private sealed class StaticTriggerMetadataAccessor : IOrchestrationTriggerMetadataAccessor
    {
        public ValueTask<OrchestrationTriggerMetadata> GetAsync(CancellationToken cancellationToken = default)
            => new(new OrchestrationTriggerMetadata { CorrelationId = "static-correlation" });
    }
}
