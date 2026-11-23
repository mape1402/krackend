using System.Security.Cryptography;
using System.Text;
using Krackend.Demo;
using Krackend.Sagas.Orchestrations.Abstractions.Artifacts;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
namespace Krackend.Demo.RuntimeHost;

public static class DemoArtifactFactory
{
    public static Id StableId(string key) => new(new Ulid(SHA256.HashData(Encoding.UTF8.GetBytes(key)).AsSpan(0, 16)));
    public static OrchestrationArtifact Create(DemoCase definition)
    {
        var artifactVersion = new SemanticVersion(1, 0, 1);
        var contractVersion = new SemanticVersion(1, 0, 0);
        var definitionId = StableId(definition.Key);
        var stages = definition.Stages.Select((operations, stageIndex) =>
        {
            var stageKey = "stage" + (stageIndex + 1);
            var group = operations.Length > 1 ? StableId(definition.Key + stageKey + ":parallel") : (Id?)null;
            var tasks = operations.Select((operation, taskIndex) =>
            {
                var compensation = operation switch
                {
                    "sales.create" => "sales.abort",
                    "sales.claim-payment" or "sales.authorize-exchange" => "sales.release-resolution",
                    "inventories.reserve" => "inventories.release",
                    "inventories.reserve-exchange" => "inventories.release-exchange",
                    "inventories.commit" => "inventories.restore",
                    "payments.capture" or "payments.confirm" => "payments.refund",
                    "payments.intent" => "payments.cancel-intent",
                    _ => null
                };
                return new TaskArtifact(StableId(definition.Key + stageKey + operation), "task" + (taskIndex + 1),
                    operation, taskIndex + 1, "", TaskKind.Messaging,
                    group.HasValue ? TaskExecutionMode.Parallel : TaskExecutionMode.Sequential, group, Condition(),
                    Transform(operation), Configuration(operation, contractVersion), null,
                    Timeout(), operation != "email.send" && (definition.Id is "UC01" or "UC02" or "UC03" or "UC04" ||
                        definition.Id == "UC07" && operation == "payments.adjust") ? OnErrorPolicy.StopAndCompensate : OnErrorPolicy.Stop,
                    compensation is null ? null : new CompensationArtifact(TaskKind.Messaging, Transform(compensation),
                        Condition(), Configuration(compensation, contractVersion), null, Timeout(), TaskDispatchType.FireAndForget),
                    TaskDispatchType.FireAndWaitCallback, true);
            }).ToArray();
            return new StageArtifact(StableId(definition.Key + stageKey), stageKey, stageKey, stageIndex + 1,
                true, Condition(), tasks, group.HasValue ? [new ParallelGroupArtifact(group.Value, ParallelJoinPolicy.WaitAll, null)] : [], []);
        }).ToArray();
        var triggerId = StableId(definition.Key + ":trigger");
        return new OrchestrationArtifact(definitionId, StableId(definition.Key + ":v1.0.1"), definition.Key, definition.Key,
            "demo", artifactVersion, new Checksum(DemoBusinessStore.Hash(new
            {
                Definition = definition,
                TriggerArchitecture = "direct-input-v2"
            })),
            [new TriggerBindingArtifact(triggerId, TriggerType.Event,
                new EventTriggerChannelArtifact(new SchemaBindingArtifact(triggerId, ElementType.Orchestration, definitionId,
                    triggerId, definition.InputTopic, contractVersion, StableId("demo-registry"), false) { IsValidationEnabled = false },
                    definition.InputTopic, contractVersion), true, definition.Id)], [], stages);
    }
    private static MessagingTaskConfigurationArtifact Configuration(string operation, SemanticVersion version)
        => new("commands.demo." + operation, version, null!);
    private static ExecutionConditionArtifact Condition()
        => new(EngineType.DSL, new DslConditionConfigurationArtifact(new Expression("true"))) { IsEnabled = false };
    private static TransformationArtifact Transform(string operation)
        => new(EngineType.DSL, new DslTransformationConfigurationArtifact
            { Dsl = "target {\n Operation: \"" + operation + "\"\n Request: $trigger\n" +
                (operation == "email.send" ? " Inputs: $responses\n" : "") + "}" }) { IsEnabled = true };
    private static TimeoutPolicyArtifact Timeout()
        => new(Duration.FromSeconds(45), TimeoutBehavior.Fail, new FailTimeoutBehaviorPolicyArtifact("DEMO_TIMEOUT"));
}

