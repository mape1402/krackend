namespace Krackend.Sagas.Orchestrations.Tests.Runtime;

using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Dispatching;
using System.Text.Json.Nodes;

public sealed class RemoteCommandMetadataBuilderTests
{
    [Fact]
    public void Build_IncludesMessageEnvelopeAndFlattenedMetadataOnly()
    {
        var propagation = new OrchestrationPropagationMetadata
        {
            Items =
            {
                [OrchestrationMetadataConstants.TriggerMetadataKey] = JsonNode.Parse("""{"CorrelationId":"corr-1"}""")!,
                ["audit.context"] = JsonNode.Parse("""{"requestId":"req-1"}""")!,
                ["security.context"] = JsonNode.Parse("""{"tenant":"north"}""")!,
                [OrchestrationMetadataConstants.OrchestrationPayloadWasNullMetadataKey] = JsonValue.Create(true)!
            }
        };
        var message = new OrchestrationMessageMetadata
        {
            SagaId = "saga-1",
            OrchestrationInstanceId = "instance-1",
            CurrentStage = "stage_one",
            CurrentTasks = ["task_one"],
            CorrelationId = "corr-1",
            TaskExecutionId = "task-execution-1",
            DispatchId = "dispatch-1",
            Attempt = 2
        };

        var metadata = RemoteCommandMetadataBuilder.Build(message, propagation);

        Assert.Equal(
            "corr-1",
            metadata[OrchestrationMetadataConstants.OrchestrationMessageMetadataKey]!["correlationId"]!.GetValue<string>());
        Assert.Equal("corr-1", metadata[OrchestrationMetadataConstants.TriggerMetadataKey]!["CorrelationId"]!.GetValue<string>());
        Assert.Equal("req-1", metadata["audit.context"]!["requestId"]!.GetValue<string>());
        Assert.Equal("north", metadata["security.context"]!["tenant"]!.GetValue<string>());
        Assert.False(metadata.ContainsKey(OrchestrationMetadataConstants.OrchestrationPropagationMetadataKey));
        Assert.False(metadata.ContainsKey(OrchestrationMetadataConstants.OrchestrationPayloadWasNullMetadataKey));
    }

    [Fact]
    public void Build_ClonesPropagationMetadataValues()
    {
        var propagation = new OrchestrationPropagationMetadata
        {
            Items =
            {
                ["audit.context"] = JsonNode.Parse("""{"requestId":"req-before"}""")!
            }
        };

        var metadata = RemoteCommandMetadataBuilder.Build(new OrchestrationMessageMetadata(), propagation);
        propagation.Items["audit.context"]!["requestId"] = "req-after";

        Assert.Equal("req-before", metadata["audit.context"]!["requestId"]!.GetValue<string>());
    }

    [Fact]
    public void Build_HandlesEmptyInputsReservedKeysAndReplyAddressMetadata()
    {
        var empty = RemoteCommandMetadataBuilder.Build(null!, null!);
        var messageOnly = RemoteCommandMetadataBuilder.Build(
            new OrchestrationMessageMetadata
            {
                ReplyAddress = new OrchestrationReplyAddress
                {
                    Transport = "messaging",
                    SettingsPayload = "{}"
                }
            },
            new OrchestrationPropagationMetadata
            {
                Items =
                {
                    [" "] = JsonValue.Create("blank")!,
                    [OrchestrationMetadataConstants.OrchestrationPropagationMetadataKey] = JsonValue.Create("reserved")!,
                    [OrchestrationMetadataConstants.TriggerMetadataKey] = JsonNode.Parse("""{"traceId":"trace-1"}""")!,
                    ["audit.context"] = null!
                }
            });

        Assert.Empty(empty);
        Assert.True(messageOnly.ContainsKey(OrchestrationMetadataConstants.OrchestrationMessageMetadataKey));
        Assert.Equal("messaging", messageOnly[OrchestrationMetadataConstants.OrchestrationMessageMetadataKey]!["replyAddress"]!["transport"]!.GetValue<string>());
        Assert.False(messageOnly.ContainsKey(" "));
        Assert.False(messageOnly.ContainsKey(OrchestrationMetadataConstants.OrchestrationPropagationMetadataKey));
        Assert.Equal("trace-1", messageOnly[OrchestrationMetadataConstants.TriggerMetadataKey]!["traceId"]!.GetValue<string>());
        Assert.True(messageOnly.ContainsKey("audit.context"));
        Assert.Null(messageOnly["audit.context"]);
        Assert.False(RemoteCommandMetadataBuilder.IsReserved(null!));
        Assert.False(RemoteCommandMetadataBuilder.IsReserved(string.Empty));
        Assert.False(RemoteCommandMetadataBuilder.IsReserved(OrchestrationMetadataConstants.TriggerMetadataKey));
        Assert.True(RemoteCommandMetadataBuilder.IsReserved(OrchestrationMetadataConstants.OrchestrationMessageMetadataKey));
    }

    [Fact]
    public void Build_IncludesMessageEnvelopeWhenAnyMessageMetadataFieldIsPresent()
    {
        var cases = new[]
        {
            new OrchestrationMessageMetadata { OrchestrationInstanceId = "instance-1" },
            new OrchestrationMessageMetadata { CurrentStage = "stage-1" },
            new OrchestrationMessageMetadata { CurrentTasks = ["task-1"] },
            new OrchestrationMessageMetadata { CorrelationId = "corr-1" },
            new OrchestrationMessageMetadata { TaskExecutionId = "task-execution-1" },
            new OrchestrationMessageMetadata { DispatchId = "dispatch-1" },
            new OrchestrationMessageMetadata { Attempt = 1 }
        };

        foreach (var message in cases)
        {
            var metadata = RemoteCommandMetadataBuilder.Build(message, null!);

            Assert.True(metadata.ContainsKey(OrchestrationMetadataConstants.OrchestrationMessageMetadataKey));
        }
    }
}
