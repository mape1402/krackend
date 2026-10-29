namespace Krackend.Sagas.Orchestrations.Tests.Messaging;

using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;
using Krackend.Sagas.Orchestrations.Client.Publishing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Pigeon.Messaging.Consuming.Dispatching;
using SquirrelBox;
using SquirrelBox.Messaging;
using SquirrelBox.Messaging.Pigeon;
using SquirrelBox.Mule;
using System.Collections.Concurrent;
using System.Reflection;

/// <summary>
/// Exercises the production SquirrelBox Pigeon decision mapper and the Krackend
/// Pigeon observer together. The broker does not own either decision: once
/// SquirrelBox rejects an inbox opening, the business handler is not reached and
/// Krackend immediately publishes the typed orchestration failure callback.
/// </summary>
public sealed class PigeonSquirrelBoxTerminalDecisionIntegrationTests
{
    private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    [Fact]
    public async Task MissingIdempotencyRejectionPublishesATypedFailureBeforeBusinessHandling()
    {
        var inboxMessages = Substitute.For<IInboxMessageService>();
        inboxMessages.OpenAsync(Arg.Any<InboxMessageContext>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(CreateRejectedInboxOpen()));

        var squirrelBoxInterceptor = new SquirrelBoxPigeonDecisionInterceptor(
            inboxMessages,
            Substitute.For<IInboxMuleScheduler>(),
            Substitute.For<IInboxService>(),
            Substitute.For<IPigeonConsumeEnvelopeFactory>(),
            Options.Create(new SquirrelBoxPigeonOptions()));

        var messageScope = new MessageMetadataScope(new OrchestrationMessageMetadata());
        var resultScope = new ResultMetadataScope();
        var propagationScope = new PropagationMetadataScope();
        var callbackPublisher = new RecordingPublisher(messageScope, resultScope, propagationScope);
        var krackendObserver = CreateKrackendObserver(callbackPublisher, messageScope, resultScope, propagationScope);

        var context = new ConsumeContext
        {
            Topic = "orders",
            Subscription = "billing",
            Message = new { Id = "order-1" },
            MessageType = typeof(object),
            MessageId = null,
            CorrelationId = "correlation-1",
            RawMetadata = new Dictionary<string, string>()
        };
        var orchestrationMetadata = new OrchestrationMessageMetadata
        {
            SagaId = "saga-1",
            OrchestrationInstanceId = "instance-1",
            CorrelationId = "correlation-1",
            TaskExecutionId = "task-1",
            Attempt = 1,
            ReplyAddress = new OrchestrationReplyAddress
            {
                Transport = OrchestrationTransportNames.Messaging,
                SettingsPayload = "{\"topic\":\"orders.reply\",\"version\":\"1.0.0\"}"
            }
        };
        SetOrchestrationMetadata(context, orchestrationMetadata);

        var decision = await squirrelBoxInterceptor.InterceptAsync(context);
        var businessHandlerExecuted = false;
        if (decision.Decision == PigeonConsumeDecision.Continue)
            businessHandlerExecuted = true;

        await krackendObserver.ObserveAsync(new ConsumeDecisionObservation(context, decision), CancellationToken.None);

        Assert.Equal(PigeonConsumeDecision.Reject, decision.Decision);
        Assert.False(businessHandlerExecuted);
        Assert.Equal(1, callbackPublisher.PublishCount);
        Assert.Same(orchestrationMetadata.ReplyAddress, callbackPublisher.Address);
        Assert.False(callbackPublisher.Result!.Succeeded);
        Assert.Equal("Rejected", callbackPublisher.Result.Status);
        Assert.Equal("Pigeon.Consume.Rejected", callbackPublisher.Result.ErrorCode);
        Assert.Null(callbackPublisher.Result.ErrorType);
        Assert.False(callbackPublisher.Result.IsRetryableCandidate);
        Assert.NotNull(callbackPublisher.Result.CompletedOnUtc);
        Assert.Equal("saga-1", callbackPublisher.Message!.SagaId);
        Assert.Equal("instance-1", callbackPublisher.Message.OrchestrationInstanceId);
        Assert.Equal("task-1", callbackPublisher.Message.TaskExecutionId);
    }

    private static InboxMessageOpenResult CreateRejectedInboxOpen()
    {
        var open = new InboxOpenResult(InboxOpenState.MissingIdempotencyKey);
        var decision = new InboxDecision(
            InboxOpenState.MissingIdempotencyKey,
            InboxPolicyAction.Reject,
            null!);
        return new InboxMessageOpenResult(open, decision);
    }

    private static IConsumeDecisionObserver CreateKrackendObserver(
        RecordingPublisher publisher,
        MessageMetadataScope messageScope,
        ResultMetadataScope resultScope,
        PropagationMetadataScope propagationScope)
    {
        var type = Assembly.Load("Krackend.Sagas.Orchestrations.Client.Messaging.Pigeon").GetType(
            "Krackend.Sagas.Orchestrations.Client.Messaging.Pigeon.KrackendPigeonConsumeDecisionObserver",
            throwOnError: true)!;
        return (IConsumeDecisionObserver)Activator.CreateInstance(
            type,
            InstanceFlags,
            binder: null,
            args:
            [
                publisher,
                messageScope,
                messageScope,
                resultScope,
                resultScope,
                propagationScope,
                propagationScope,
                NullLogger.Instance
            ],
            culture: null)!;
    }

    private static void SetOrchestrationMetadata(ConsumeContext context, OrchestrationMessageMetadata metadata)
    {
        var field = typeof(ConsumeContext).GetField("_metadata", InstanceFlags)!;
        var values = (ConcurrentDictionary<string, object>)field.GetValue(context)!;
        values[OrchestrationMetadataConstants.OrchestrationMessageMetadataKey] = metadata;
    }

    private sealed class RecordingPublisher : IOrchestrationClientPublisher
    {
        private readonly MessageMetadataScope _messageScope;
        private readonly ResultMetadataScope _resultScope;
        private readonly PropagationMetadataScope _propagationScope;

        public RecordingPublisher(
            MessageMetadataScope messageScope,
            ResultMetadataScope resultScope,
            PropagationMetadataScope propagationScope)
        {
            _messageScope = messageScope;
            _resultScope = resultScope;
            _propagationScope = propagationScope;
        }

        public int PublishCount { get; private set; }
        public OrchestrationReplyAddress? Address { get; private set; }
        public OrchestrationMessageMetadata? Message { get; private set; }
        public OrchestrationExecutionResultMetadata? Result { get; private set; }

        public Task PublishAsync(object payload, OrchestrationReplyAddress address, CancellationToken cancellationToken = default)
        {
            PublishCount++;
            Address = address;
            Message = _messageScope.Value;
            Result = _resultScope.Value;
            _ = _propagationScope.Value;
            return Task.CompletedTask;
        }
    }

    private sealed class MessageMetadataScope : IOrchestrationMessageMetadataAccessor, IOrchestrationMessageMetadataSetter
    {
        public MessageMetadataScope(OrchestrationMessageMetadata value) => Value = value;
        public OrchestrationMessageMetadata Value { get; private set; }
        public OrchestrationMessageMetadata Get() => Value;
        public void Set(OrchestrationMessageMetadata metadata) => Value = metadata;
    }

    private sealed class ResultMetadataScope : IOrchestrationExecutionResultMetadataAccessor, IOrchestrationExecutionResultMetadataSetter
    {
        public OrchestrationExecutionResultMetadata? Value { get; private set; }
        public OrchestrationExecutionResultMetadata Get() => Value!;
        public void Set(OrchestrationExecutionResultMetadata metadata) => Value = metadata;
        public void Clear() => Value = null;
    }

    private sealed class PropagationMetadataScope : IOrchestrationPropagationMetadataAccessor, IOrchestrationPropagationMetadataSetter
    {
        public OrchestrationPropagationMetadata Value { get; private set; } = new();
        public OrchestrationPropagationMetadata Get() => Value.Clone();
        public void Set(OrchestrationPropagationMetadata metadata) => Value = metadata.Clone();
        public void Clear() => Value = new OrchestrationPropagationMetadata();
    }
}
