namespace Krackend.Sagas.Orchestrations.Tests.Messaging;

using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;
using Krackend.Sagas.Orchestrations.Client.Publishing;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Pigeon.Messaging.Consuming.Dispatching;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json.Nodes;

public sealed class PigeonConsumeDecisionObserverTests
{
    private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    [Fact]
    public async Task RejectedDecisionPublishesTypedFailureAndRestoresTheScope()
    {
        var priorMessage = new OrchestrationMessageMetadata { CorrelationId = "prior" };
        var priorResult = new OrchestrationExecutionResultMetadata { Status = "prior" };
        var priorPropagation = new OrchestrationPropagationMetadata
        {
            Items = { ["prior.context"] = JsonValue.Create("prior") }
        };
        var messageScope = new MessageMetadataScope(priorMessage);
        var resultScope = new ResultMetadataScope(priorResult);
        var propagationScope = new PropagationMetadataScope(priorPropagation);
        var publisher = new RecordingPublisher(messageScope, resultScope, propagationScope);
        var observer = CreateObserver(publisher, messageScope, resultScope, propagationScope);
        var orchestrationMetadata = CreateOrchestrationMetadata();
        var context = new ConsumeContext
        {
            RawMetadata = new Dictionary<string, string>
            {
                ["SquirrelBoxMetadata"] = "{\"inbox\":\"inbox-1\"}",
                ["audit.context"] = "{\"request\":\"req-1\"}"
            }
        };
        context.ReplyMetadata["SquirrelBoxMetadata"] = "{\"outbox\":\"outbox-1\"}";
        SetOrchestrationMetadata(context, orchestrationMetadata);
        var observation = new ConsumeDecisionObservation(
            context,
            new PigeonConsumeDecisionResult(PigeonConsumeDecision.Reject, "handler rejected")
            {
                Outcome = new PigeonConsumeOutcome
                {
                    Kind = PigeonConsumeOutcomeKind.Rejected,
                    Failure = new PigeonConsumeFailure
                    {
                        Code = "ValidationFailed",
                        Type = "ValidationException",
                        Message = "A required field is missing.",
                        // The observer must still make a terminal Pigeon
                        // rejection non-retryable for the orchestration.
                        IsRetryable = true
                    }
                },
                Metadata = new Dictionary<string, string>
                {
                    ["SquirrelBoxMetadata"] = "{\"decision\":\"saved\"}"
                }
            });

        await observer.ObserveAsync(observation, CancellationToken.None);

        Assert.Equal(1, publisher.PublishCount);
        Assert.Null(publisher.Payload);
        Assert.Same(orchestrationMetadata.ReplyAddress, publisher.Address);
        Assert.Same(orchestrationMetadata, publisher.MessageMetadata);
        Assert.False(publisher.ResultMetadata!.Succeeded);
        Assert.Equal("Rejected", publisher.ResultMetadata.Status);
        Assert.Equal("ValidationFailed", publisher.ResultMetadata.ErrorCode);
        Assert.Equal("ValidationException", publisher.ResultMetadata.ErrorType);
        Assert.Equal("A required field is missing.", publisher.ResultMetadata.ErrorMessage);
        Assert.False(publisher.ResultMetadata.IsRetryableCandidate);
        Assert.NotNull(publisher.ResultMetadata.CompletedOnUtc);
        Assert.Equal("{\"decision\":\"saved\"}", publisher.ResultMetadata.Metadata["Krackend.Pigeon.Decision.Metadata"]!["SquirrelBoxMetadata"]!.GetValue<string>());
        Assert.Equal("{\"outbox\":\"outbox-1\"}", publisher.ResultMetadata.Metadata["Krackend.Pigeon.Reply.Metadata"]!["SquirrelBoxMetadata"]!.GetValue<string>());
        Assert.Equal("inbox-1", publisher.PropagationMetadata!.Items["SquirrelBoxMetadata"]!["inbox"]!.GetValue<string>());
        Assert.Equal("req-1", publisher.PropagationMetadata.Items["audit.context"]!["request"]!.GetValue<string>());
        Assert.Same(priorMessage, messageScope.Value);
        Assert.Same(priorResult, resultScope.Value);
        Assert.Equal("prior", propagationScope.Value.Items["prior.context"]!.GetValue<string>());
    }

    [Theory]
    [InlineData(PigeonConsumeDecision.Continue)]
    [InlineData(PigeonConsumeDecision.AckAndSkip)]
    [InlineData(PigeonConsumeDecision.Retry)]
    [InlineData(PigeonConsumeDecision.Defer)]
    public async Task NonRejectedDecisionsDoNotPublishAnOrchestrationFailure(PigeonConsumeDecision decision)
    {
        var messageScope = new MessageMetadataScope(new OrchestrationMessageMetadata());
        var resultScope = new ResultMetadataScope(null);
        var propagationScope = new PropagationMetadataScope(new OrchestrationPropagationMetadata());
        var publisher = new RecordingPublisher(messageScope, resultScope, propagationScope);
        var observer = CreateObserver(publisher, messageScope, resultScope, propagationScope);
        var context = new ConsumeContext();
        SetOrchestrationMetadata(context, CreateOrchestrationMetadata());

        await observer.ObserveAsync(
            new ConsumeDecisionObservation(context, new PigeonConsumeDecisionResult(decision, "not terminal")),
            CancellationToken.None);

        Assert.Equal(0, publisher.PublishCount);
    }

    [Fact]
    public async Task RejectedDecisionWithoutAReplyAddressIsIgnored()
    {
        var messageScope = new MessageMetadataScope(new OrchestrationMessageMetadata());
        var resultScope = new ResultMetadataScope(null);
        var propagationScope = new PropagationMetadataScope(new OrchestrationPropagationMetadata());
        var publisher = new RecordingPublisher(messageScope, resultScope, propagationScope);
        var observer = CreateObserver(publisher, messageScope, resultScope, propagationScope);
        var context = new ConsumeContext();
        SetOrchestrationMetadata(context, new OrchestrationMessageMetadata { SagaId = "saga-1" });

        await observer.ObserveAsync(
            new ConsumeDecisionObservation(context, new PigeonConsumeDecisionResult(PigeonConsumeDecision.Reject, "rejected")),
            CancellationToken.None);

        Assert.Equal(0, publisher.PublishCount);
    }

    private static IConsumeDecisionObserver CreateObserver(
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

    private static OrchestrationMessageMetadata CreateOrchestrationMetadata()
        => new()
        {
            SagaId = "saga-1",
            OrchestrationInstanceId = "instance-1",
            CorrelationId = "correlation-1",
            TaskExecutionId = "task-1",
            Attempt = 3,
            ReplyAddress = new OrchestrationReplyAddress
            {
                Transport = OrchestrationTransportNames.Messaging,
                SettingsPayload = "{\"topic\":\"sales.reply\",\"version\":\"1.0.0\"}"
            }
        };

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
        public object? Payload { get; private set; }
        public OrchestrationReplyAddress? Address { get; private set; }
        public OrchestrationMessageMetadata? MessageMetadata { get; private set; }
        public OrchestrationExecutionResultMetadata? ResultMetadata { get; private set; }
        public OrchestrationPropagationMetadata? PropagationMetadata { get; private set; }

        public Task PublishAsync(object payload, OrchestrationReplyAddress address, CancellationToken cancellationToken = default)
        {
            PublishCount++;
            Payload = payload;
            Address = address;
            MessageMetadata = _messageScope.Value;
            ResultMetadata = _resultScope.Value;
            PropagationMetadata = _propagationScope.Value.Clone();
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
        public ResultMetadataScope(OrchestrationExecutionResultMetadata? value) => Value = value;
        public OrchestrationExecutionResultMetadata? Value { get; private set; }
        public OrchestrationExecutionResultMetadata Get() => Value!;
        public void Set(OrchestrationExecutionResultMetadata metadata) => Value = metadata;
        public void Clear() => Value = null;
    }

    private sealed class PropagationMetadataScope : IOrchestrationPropagationMetadataAccessor, IOrchestrationPropagationMetadataSetter
    {
        public PropagationMetadataScope(OrchestrationPropagationMetadata value) => Value = value.Clone();
        public OrchestrationPropagationMetadata Value { get; private set; }
        public OrchestrationPropagationMetadata Get() => Value.Clone();
        public void Set(OrchestrationPropagationMetadata metadata) => Value = metadata.Clone();
        public void Clear() => Value = new OrchestrationPropagationMetadata();
    }
}
