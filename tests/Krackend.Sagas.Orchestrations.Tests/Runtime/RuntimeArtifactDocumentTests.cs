using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Engine;

namespace Krackend.Sagas.Orchestrations.Tests.Runtime;

public sealed class RuntimeArtifactDocumentTests
{
    [Fact]
    public void Parse_ReadsFullMessagingTaskConfigurationAndUsesDispatchTypeForAwaitResponse()
    {
        var payload = JsonNode.Parse($$"""
        {
          "Key": "order.fulfillment",
          "Version": { "Major": 2, "Minor": 1, "Patch": 0 },
          "TriggerBindings": [
            {
              "IsEnabled": true,
              "TriggerChannel": {
                "Topic": "orders.created",
                "Version": { "Major": 1, "Minor": 0, "Patch": 0 }
              }
            }
          ],
          "VariableDefinitions": [
            { "Key": "inventoryTimeout", "Scope": {{(int)VariableScope.Environment}} }
          ],
          "StageDefinitions": [
            {
              "Key": "reserve-inventory",
              "Order": 1,
              "ExecutionCondition": { "Engine": {{(int)EngineType.DSL}}, "Configuration": { "Expression": { "Value": "payload.total > 0" } } },
              "ParallelGroups": [
                { "Id": "01K00000000000000000000000", "JoinPolicy": {{(int)ParallelJoinPolicy.WaitAll}}, "MaxParallelAgents": 2 }
              ],
              "BranchRules": [
                { "FromType": {{(int)ElementType.Stage}}, "NavigateToType": {{(int)ElementType.Stage}} }
              ],
              "TaskDefinitions": [
                {
                  "Key": "reserve-stock",
                  "Order": 1,
                  "Kind": {{(int)TaskKind.Messaging}},
                  "ExecutionMode": {{(int)TaskExecutionMode.Parallel}},
                  "DispatchType": {{(int)TaskDispatchType.FireAndWaitCallback}},
                  "ParallelGroupId": "01K00000000000000000000001",
                  "ExecutionCondition": { "Engine": {{(int)EngineType.DSL}} },
                  "Transformation": { "Engine": {{(int)EngineType.DSL}} },
                  "Configuration": {
                    "$artifactType": "messaging",
                    "Topic": "inventory.reserve",
                    "Version": { "Major": 1, "Minor": 2, "Patch": 3 }
                  },
                  "RetryPolicy": {
                    "MaxRetries": 3,
                    "StrategyType": {{(int)RetryStrategyType.Fixed}}
                  },
                  "TimeoutPolicy": {
                    "TimeoutBehavior": {{(int)TimeoutBehavior.Reconcile}}
                  },
                  "OnErrorPolicy": {{(int)OnErrorPolicy.StopAndCompensate}},
                  "Compensation": {
                    "CompensationTaskKind": {{(int)TaskKind.Messaging}},
                    "Configuration": { "Topic": "inventory.release" }
                  },
                  "IsEnabled": true
                }
              ]
            }
          ]
        }
        """);

        var document = RuntimeArtifactDocument.Parse(payload!, "9.9.9");
        var stage = Assert.Single(document.Stages);
        var task = Assert.Single(stage.Tasks);

        Assert.Equal("order.fulfillment", document.Key);
        Assert.Equal("2.1.0", document.Version);
        Assert.Single(document.TriggerBindings);
        Assert.Single(document.VariableDefinitions);
        Assert.Equal("reserve-inventory", stage.Key);
        Assert.False(stage.ExecutionCondition.Count == 0);
        Assert.Single(stage.ParallelGroups);
        Assert.Single(stage.BranchRules);
        Assert.Equal("reserve-stock", task.Key);
        Assert.Equal(nameof(TaskKind.Messaging), task.Kind);
        Assert.Equal(nameof(TaskExecutionMode.Parallel), task.ExecutionMode);
        Assert.Equal(nameof(TaskDispatchType.FireAndWaitCallback), task.DispatchType);
        Assert.True(task.AwaitResponse);
        Assert.Equal("inventory.reserve", task.Destination);
        Assert.Equal("1.2.3", task.MessageVersion);
        Assert.Equal(nameof(OnErrorPolicy.StopAndCompensate), task.OnErrorPolicy);
        Assert.False(task.ExecutionCondition.Count == 0);
        Assert.False(task.Transformation.Count == 0);
        Assert.False(task.RetryPolicy.Count == 0);
        Assert.False(task.TimeoutPolicy.Count == 0);
        Assert.False(task.Compensation.Count == 0);
    }

    [Fact]
    public void Parse_DoesNotWaitForResponseWhenExecutionModeLooksParallelButDispatchIsFireAndForget()
    {
        var payload = JsonNode.Parse($$"""
        {
          "Key": "order.fulfillment",
          "StageDefinitions": [
            {
              "Key": "reserve-inventory",
              "Order": 1,
              "TaskDefinitions": [
                {
                  "Key": "reserve-stock",
                  "Order": 1,
                  "Kind": {{(int)TaskKind.Messaging}},
                  "ExecutionMode": {{(int)TaskExecutionMode.Parallel}},
                  "DispatchType": {{(int)TaskDispatchType.FireAndForget}},
                  "Configuration": { "Topic": "inventory.reserve", "Version": "1.0.0" },
                  "IsEnabled": true
                }
              ]
            }
          ]
        }
        """);

        var document = RuntimeArtifactDocument.Parse(payload!, "1.0.0");
        var task = Assert.Single(Assert.Single(document.Stages).Tasks);

        Assert.Equal(nameof(TaskExecutionMode.Parallel), task.ExecutionMode);
        Assert.Equal(nameof(TaskDispatchType.FireAndForget), task.DispatchType);
        Assert.False(task.AwaitResponse);
    }
}
