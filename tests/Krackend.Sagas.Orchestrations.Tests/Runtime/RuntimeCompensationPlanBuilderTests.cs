using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Engine;

namespace Krackend.Sagas.Orchestrations.Tests.Runtime;

public sealed class RuntimeCompensationPlanBuilderTests
{
    [Fact]
    public void Build_UsesCompletedTasksInReverseArtifactOrder()
    {
        var document = RuntimeArtifactDocument.Parse(JsonNode.Parse($$"""
        {
          "Key": "order.fulfillment",
          "Version": { "Major": 1, "Minor": 0, "Patch": 0 },
          "Stages": [
            {
              "Key": "reserve",
              "Order": 1,
              "Tasks": [
                {
                  "Key": "reserve-stock",
                  "Order": 1,
                  "Kind": {{(int)TaskKind.Messaging}},
                  "DispatchType": {{(int)TaskDispatchType.FireAndWaitCallback}},
                  "IsEnabled": true,
                  "Compensation": {
                    "CompensationTaskKind": {{(int)TaskKind.Messaging}},
                    "DispatchType": {{(int)TaskDispatchType.FireAndForget}},
                    "Configuration": { "Topic": "inventory.release", "Version": { "Major": 1, "Minor": 0, "Patch": 0 } }
                  }
                }
              ]
            },
            {
              "Key": "payment",
              "Order": 2,
              "Tasks": [
                {
                  "Key": "charge-card",
                  "Order": 1,
                  "Kind": {{(int)TaskKind.Messaging}},
                  "DispatchType": {{(int)TaskDispatchType.FireAndWaitCallback}},
                  "IsEnabled": true,
                  "Compensation": {
                    "CompensationTaskKind": {{(int)TaskKind.Messaging}},
                    "DispatchType": {{(int)TaskDispatchType.FireAndForget}},
                    "Configuration": { "Topic": "payment.refund", "Version": { "Major": 1, "Minor": 0, "Patch": 0 } }
                  }
                }
              ]
            }
          ]
        }
        """))!;

        var completedTasks = new[]
        {
            CompletedTask("reserve-stock", """{"reservationId":"r1"}"""),
            CompletedTask("charge-card", """{"paymentId":"p1"}""")
        };

        var plan = new RuntimeCompensationPlanBuilder().Build(document, completedTasks).ToArray();

        Assert.Equal(["charge-card", "reserve-stock"], plan.Select(x => x.SourceTaskKey));
        Assert.Equal("payment.refund", plan[0].Destination);
        Assert.Equal("inventory.release", plan[1].Destination);
        Assert.Equal("1.0.0", plan[0].MessageVersion);
    }

    [Fact]
    public void Build_IgnoresTasksWithoutMessagingCompensationDestination()
    {
        var document = RuntimeArtifactDocument.Parse(JsonNode.Parse($$"""
        {
          "Key": "order.fulfillment",
          "Stages": [
            {
              "Key": "reserve",
              "Order": 1,
              "Tasks": [
                {
                  "Key": "reserve-stock",
                  "Order": 1,
                  "Kind": {{(int)TaskKind.Messaging}},
                  "IsEnabled": true,
                  "Compensation": {
                    "CompensationTaskKind": {{(int)TaskKind.Messaging}},
                    "Configuration": { "Topic": "" }
                  }
                }
              ]
            }
          ]
        }
        """))!;

        var plan = new RuntimeCompensationPlanBuilder().Build(
            document,
            [CompletedTask("reserve-stock", """{"reservationId":"r1"}""")]);

        Assert.Empty(plan);
    }

    private static TaskExecution CompletedTask(string key, string outputJson)
        => new()
        {
            Id = Id.New(),
            OrchestrationInstanceId = Id.New(),
            StageExecutionId = Id.New(),
            TaskKey = key,
            TaskKind = TaskKind.Messaging,
            Status = TaskExecutionStatus.Completed,
            CompletedOnUtc = DateTime.UtcNow,
            OutputVariablesPayload = JsonNode.Parse(outputJson)
        };
}
