namespace Krackend.Sagas.Orchestrations.Tests.Client;

using System.Reflection;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;
using Krackend.Sagas.Orchestrations.Client.Publishing;
using System.Text.Json.Nodes;

public sealed class OrchestrationMessageMetadataComposerTests
{
    private static readonly Type ComposerType = typeof(OrchestrationOperationOptions).Assembly.GetType(
        "Krackend.Sagas.Orchestrations.Client.Publishing.DefaultOrchestrationMessageMetadataComposer",
        throwOnError: true)!;

    public static IEnumerable<object[]> OriginMetadataCases =>
    [
        [new Action<OrchestrationMessageMetadata>(metadata => metadata.SagaId = "saga-1")],
        [new Action<OrchestrationMessageMetadata>(metadata => metadata.OrchestrationInstanceId = "instance-1")],
        [new Action<OrchestrationMessageMetadata>(metadata => metadata.CurrentStage = "stage-1")],
        [new Action<OrchestrationMessageMetadata>(metadata => metadata.CurrentTasks = ["task-1"])],
        [new Action<OrchestrationMessageMetadata>(metadata => metadata.TaskExecutionId = "task-execution-1")],
        [new Action<OrchestrationMessageMetadata>(metadata => metadata.DispatchId = "dispatch-1")],
        [new Action<OrchestrationMessageMetadata>(metadata => metadata.Attempt = 1)]
    ];

    [Fact]
    public void ComposeEventPropagationMetadata_WithNullInputs_GeneratesTriggerOnly()
    {
        var composer = CreateComposer();

        var propagation = ComposePropagation(composer, null, null, null, null);
        var messageMetadata = ComposeMessage(composer, null, null, null, null);
        var trigger = propagation.Items[OrchestrationMetadataConstants.TriggerMetadataKey]!;
        var eventId = trigger[nameof(OrchestrationTriggerMetadata.EventId)]!.GetValue<string>();

        Assert.True(Ulid.TryParse(eventId, out _));
        Assert.Equal(eventId, trigger[nameof(OrchestrationTriggerMetadata.CorrelationId)]!.GetValue<string>());
        Assert.Equal(eventId, trigger[nameof(OrchestrationTriggerMetadata.IdempotencyKey)]!.GetValue<string>());
        Assert.True(Ulid.TryParse(messageMetadata.CorrelationId, out _));
        Assert.False(trigger.AsObject().ContainsKey(nameof(OrchestrationTriggerMetadata.EventType)));
        Assert.False(propagation.Items.ContainsKey(OrchestrationMetadataConstants.OriginMetadataKey));
    }

    [Fact]
    public void ComposeEventPropagationMetadata_HandlesAddressFallbacksAndValidMessagingTopic()
    {
        var composer = CreateComposer();
        var invalidAddressCases = new[]
        {
            new OrchestrationReplyAddress { Transport = "custom", SettingsPayload = "{}" },
            new OrchestrationReplyAddress { Transport = OrchestrationTransportNames.Messaging, SettingsPayload = " " },
            new OrchestrationReplyAddress { Transport = OrchestrationTransportNames.Messaging, SettingsPayload = "not-json" },
            new OrchestrationReplyAddress { Transport = OrchestrationTransportNames.Messaging, SettingsPayload = "null" },
            new OrchestrationReplyAddress { Transport = OrchestrationTransportNames.Messaging, SettingsPayload = "{}" }
        };

        foreach (var address in invalidAddressCases)
        {
            var propagation = ComposePropagation(composer, null, null, null, address);

            Assert.False(propagation.Items[OrchestrationMetadataConstants.TriggerMetadataKey]!
                .AsObject()
                .ContainsKey(nameof(OrchestrationTriggerMetadata.EventType)));
        }

        var valid = ComposePropagation(
            composer,
            null,
            null,
            null,
            new OrchestrationReplyAddress
            {
                Transport = OrchestrationTransportNames.Messaging,
                SettingsPayload = """{"topic":"events.sales.created","version":"1.0.0"}"""
            });

        Assert.Equal(
            "events.sales.created",
            valid.Items[OrchestrationMetadataConstants.TriggerMetadataKey]![nameof(OrchestrationTriggerMetadata.EventType)]!.GetValue<string>());
    }

    [Fact]
    public void ComposeEventPropagationMetadata_UsesExplicitTriggerAndRemovesLegacy()
    {
        var composer = CreateComposer();
        var previousPropagation = new OrchestrationPropagationMetadata
        {
            Items =
            {
                [OrchestrationMetadataConstants.LegacyTriggerMetadataKey] = new OrchestrationTriggerMetadata
                {
                    CorrelationId = "legacy-correlation",
                    EventId = "legacy-event"
                }.ToJson()
            }
        };

        var propagation = ComposePropagation(
            composer,
            new OrchestrationMessageMetadata { CorrelationId = "message-correlation" },
            previousPropagation,
            new OrchestrationTriggerMetadata
            {
                CorrelationId = "trigger-correlation",
                TraceId = "trigger-trace",
                EventId = "trigger-event",
                EventType = "trigger-type",
                IdempotencyKey = "trigger-idempotency",
                AggregateId = "aggregate-1",
                AggregateType = "Sale",
                CausationId = "trigger-causation"
            },
            null);

        var trigger = propagation.Items[OrchestrationMetadataConstants.TriggerMetadataKey]!;
        Assert.Equal("trigger-correlation", trigger[nameof(OrchestrationTriggerMetadata.CorrelationId)]!.GetValue<string>());
        Assert.Equal("trigger-trace", trigger[nameof(OrchestrationTriggerMetadata.TraceId)]!.GetValue<string>());
        Assert.Equal("trigger-event", trigger[nameof(OrchestrationTriggerMetadata.EventId)]!.GetValue<string>());
        Assert.Equal("trigger-type", trigger[nameof(OrchestrationTriggerMetadata.EventType)]!.GetValue<string>());
        Assert.Equal("trigger-idempotency", trigger[nameof(OrchestrationTriggerMetadata.IdempotencyKey)]!.GetValue<string>());
        Assert.Equal("aggregate-1", trigger[nameof(OrchestrationTriggerMetadata.AggregateId)]!.GetValue<string>());
        Assert.Equal("Sale", trigger[nameof(OrchestrationTriggerMetadata.AggregateType)]!.GetValue<string>());
        Assert.Equal("trigger-causation", trigger[nameof(OrchestrationTriggerMetadata.CausationId)]!.GetValue<string>());
        Assert.False(propagation.Items.ContainsKey(OrchestrationMetadataConstants.LegacyTriggerMetadataKey));
    }

    [Theory]
    [MemberData(nameof(OriginMetadataCases))]
    public void ComposeEventPropagationMetadata_AddsOriginWhenAnyMessageOriginFieldExists(
        Action<OrchestrationMessageMetadata> configure)
    {
        var composer = CreateComposer();
        var messageMetadata = new OrchestrationMessageMetadata();
        configure(messageMetadata);
        var previousPropagation = new OrchestrationPropagationMetadata
        {
            Items =
            {
                [OrchestrationMetadataConstants.TriggerMetadataKey] = new OrchestrationTriggerMetadata
                {
                    CorrelationId = "trigger-correlation",
                    TraceId = "trigger-trace"
                }.ToJson()
            }
        };

        var propagation = ComposePropagation(composer, messageMetadata, previousPropagation, null, null);
        var origin = propagation.Items[OrchestrationMetadataConstants.OriginMetadataKey]!;

        Assert.Equal("trigger-correlation", origin[nameof(OrchestrationOriginMetadata.CorrelationId)]!.GetValue<string>());
        Assert.Equal("trigger-trace", origin[nameof(OrchestrationOriginMetadata.TraceId)]!.GetValue<string>());
        Assert.Equal("SagaTask", origin[nameof(OrchestrationOriginMetadata.Source)]!.GetValue<string>());
    }

    [Fact]
    public void ComposeEventPropagationMetadata_HandlesNullPropagationItemsAndMissingTrigger()
    {
        var composer = CreateComposer();
        var nullItemsPropagation = new OrchestrationPropagationMetadata
        {
            Items = null!
        };
        var missingTriggerPropagation = new OrchestrationPropagationMetadata
        {
            Items =
            {
                ["audit.context"] = JsonValue.Create("kept")
            }
        };

        var fromNullItems = ComposePropagation(composer, null, nullItemsPropagation, null, null);
        var fromMissingTrigger = ComposePropagation(composer, null, missingTriggerPropagation, null, null);

        Assert.True(fromNullItems.Items.ContainsKey(OrchestrationMetadataConstants.TriggerMetadataKey));
        Assert.Equal("kept", fromMissingTrigger.Items["audit.context"]!.GetValue<string>());
        Assert.True(fromMissingTrigger.Items.ContainsKey(OrchestrationMetadataConstants.TriggerMetadataKey));
    }

    private static object CreateComposer()
        => Activator.CreateInstance(ComposerType, nonPublic: true)!;

    private static OrchestrationMessageMetadata ComposeMessage(
        object composer,
        OrchestrationMessageMetadata? messageMetadata,
        OrchestrationPropagationMetadata? propagationMetadata,
        OrchestrationTriggerMetadata? triggerMetadata,
        OrchestrationReplyAddress? triggerAddress)
        => (OrchestrationMessageMetadata)ComposerType
            .GetMethod(
                "ComposeEventMessageMetadata",
                BindingFlags.Instance | BindingFlags.Public,
                binder: null,
                types: [typeof(OrchestrationMessageMetadata), typeof(OrchestrationPropagationMetadata), typeof(OrchestrationTriggerMetadata), typeof(OrchestrationReplyAddress)],
                modifiers: null)!
            .Invoke(composer, [messageMetadata, propagationMetadata, triggerMetadata, triggerAddress])!;

    private static OrchestrationPropagationMetadata ComposePropagation(
        object composer,
        OrchestrationMessageMetadata? messageMetadata,
        OrchestrationPropagationMetadata? propagationMetadata,
        OrchestrationTriggerMetadata? triggerMetadata,
        OrchestrationReplyAddress? triggerAddress)
        => (OrchestrationPropagationMetadata)ComposerType
            .GetMethod(
                "ComposeEventPropagationMetadata",
                BindingFlags.Instance | BindingFlags.Public,
                binder: null,
                types: [typeof(OrchestrationMessageMetadata), typeof(OrchestrationPropagationMetadata), typeof(OrchestrationTriggerMetadata), typeof(OrchestrationReplyAddress)],
                modifiers: null)!
            .Invoke(composer, [messageMetadata, propagationMetadata, triggerMetadata, triggerAddress])!;
}
