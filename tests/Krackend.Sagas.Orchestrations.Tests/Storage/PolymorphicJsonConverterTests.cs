namespace Krackend.Sagas.Orchestrations.Tests.Storage;

using System.Text.Json;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Design.JsonModels;

public sealed class PolymorphicJsonConverterTests
{
    [Theory]
    [MemberData(nameof(TaskConfigurationCases))]
    public void TaskConfigurationConverterRoundTripsSupportedConfigurationTypes(
        TaskConfigurationJsonModel model,
        string expectedDiscriminator,
        Type expectedType)
    {
        var json = JsonSerializer.Serialize<TaskConfigurationJsonModel>(model);
        var roundTrip = JsonSerializer.Deserialize<TaskConfigurationJsonModel>(json);

        Assert.Contains($"\"$type\":\"{expectedDiscriminator}\"", json, StringComparison.OrdinalIgnoreCase);
        Assert.IsType(expectedType, roundTrip);
    }

    [Fact]
    public void TriggerTransformationAndTimeoutConvertersRoundTripSupportedTypes()
    {
        var trigger = RoundTrip<TriggerChannelJsonModel>(
            new EventTriggerChannelJsonModel { Topic = "events.sales.sale.created", Version = "1.0.0" });
        var condition = RoundTrip<ConditionConfigurationJsonModel>(
            new DslConditionConfigurationJsonModel { Expression = "trigger.total > 0" });
        var retryStrategy = RoundTrip<RetryStrategyJsonModel>(
            new FixedRetryStrategyJsonModel { Delay = TimeSpan.FromSeconds(15) });
        var transformation = RoundTrip<TransformationConfigurationJsonModel>(
            new DslTransformationConfigurationJsonModel
            {
                Dsl = "map request",
                SourceContextHash = "ctx",
                TargetSchemaHash = "target",
                SemanticDiagnosticsJson = "{}"
            });
        var fail = RoundTrip<TimeoutBehaviorPolicyJsonModel>(
            new FailTimeoutBehaviorPolicyJsonModel { ErrorCode = "TIMEOUT" });
        var wait = RoundTrip<TimeoutBehaviorPolicyJsonModel>(
            new WaitTimeoutBehaviorPolicyJsonModel
            {
                OrchestrationAction = OrchestrationActionOnTimeout.Continue,
                WaitingTime = TimeSpan.FromSeconds(5)
            });
        var reconcile = RoundTrip<TimeoutBehaviorPolicyJsonModel>(
            new ReconcileTimeoutBehaviorPolicyJsonModel
            {
                OrchestrationAction = OrchestrationActionOnTimeout.Block,
                RetryPolicy = new RetryPolicyJsonModel
                {
                    MaxRetries = 2,
                    StrategyType = RetryStrategyType.Fixed,
                    Strategy = new RetryStrategyEnvelopeJsonModel
                    {
                        Type = "fixed",
                        Fixed = new FixedRetryStrategyJsonModel { Delay = TimeSpan.FromSeconds(3) }
                    },
                    RetryableErrorCodes = ["TEMP"],
                    StopOnNonRetryableError = true
                }
            });

        Assert.IsType<EventTriggerChannelJsonModel>(trigger);
        Assert.Equal("events.sales.sale.created", ((EventTriggerChannelJsonModel)trigger).Topic);
        Assert.IsType<DslConditionConfigurationJsonModel>(condition);
        Assert.Equal("trigger.total > 0", ((DslConditionConfigurationJsonModel)condition).Expression);
        Assert.IsType<FixedRetryStrategyJsonModel>(retryStrategy);
        Assert.Equal(TimeSpan.FromSeconds(15), ((FixedRetryStrategyJsonModel)retryStrategy).Delay);
        Assert.IsType<DslTransformationConfigurationJsonModel>(transformation);
        Assert.Equal("map request", ((DslTransformationConfigurationJsonModel)transformation).Dsl);
        Assert.IsType<FailTimeoutBehaviorPolicyJsonModel>(fail);
        Assert.Equal("TIMEOUT", ((FailTimeoutBehaviorPolicyJsonModel)fail).ErrorCode);
        Assert.IsType<WaitTimeoutBehaviorPolicyJsonModel>(wait);
        Assert.Equal(TimeSpan.FromSeconds(5), ((WaitTimeoutBehaviorPolicyJsonModel)wait).WaitingTime);
        Assert.IsType<ReconcileTimeoutBehaviorPolicyJsonModel>(reconcile);
        Assert.Equal(2, ((ReconcileTimeoutBehaviorPolicyJsonModel)reconcile).RetryPolicy.MaxRetries);
    }

    [Fact]
    public void PolymorphicConvertersRejectInvalidPayloadsAndUnsupportedRuntimeTypes()
    {
        Assert.Equal("null", JsonSerializer.Serialize<TaskConfigurationJsonModel?>(null));
        Assert.Throws<JsonException>(() => JsonSerializer.Serialize<TaskConfigurationJsonModel>(new TaskConfigurationJsonModel()));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<TaskConfigurationJsonModel>("{}"));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<TaskConfigurationJsonModel>("""{"$type":"missing"}"""));
    }

    public static TheoryData<TaskConfigurationJsonModel, string, Type> TaskConfigurationCases()
        => new()
        {
            {
                new MessagingTaskConfigurationJsonModel
                {
                    Topic = "commands.inventories.reserve",
                    Version = "1.0.0"
                },
                "messaging",
                typeof(MessagingTaskConfigurationJsonModel)
            },
            {
                new HttpTaskConfigurationJsonModel
                {
                    BaseUrlVariableRef = "inventory-api",
                    RelativePath = "/reserve",
                    Method = "POST",
                    ExpectedStatusCodes = [200, 202],
                    AllowSyncResponse = true
                },
                "http",
                typeof(HttpTaskConfigurationJsonModel)
            },
            {
                new PluginTaskConfigurationJsonModel { PluginId = Id.New().ToString() },
                "plugin",
                typeof(PluginTaskConfigurationJsonModel)
            },
            {
                new HumanApprovalTaskConfigurationJsonModel(),
                "humanApproval",
                typeof(HumanApprovalTaskConfigurationJsonModel)
            }
        };

    private static TBase RoundTrip<TBase>(TBase model)
    {
        var json = JsonSerializer.Serialize(model, typeof(TBase));
        return (TBase)JsonSerializer.Deserialize(json, typeof(TBase))!;
    }
}
