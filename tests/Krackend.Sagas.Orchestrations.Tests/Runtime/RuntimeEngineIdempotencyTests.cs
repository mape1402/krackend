using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Intake;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Reactive;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Engine;

namespace Krackend.Sagas.Orchestrations.Tests.Runtime;

public sealed class RuntimeEngineIdempotencyTests
{
    [Fact]
    public async Task ProcessNext_CreatesTaskCorrelationScopedByInstanceId()
    {
        var store = new RuntimeStore();
        var artifactId = Id.New();
        var instance = new OrchestrationInstance
        {
            Id = Id.New(),
            EnvironmentKey = "local",
            OrchestrationDefinitionKey = "order.fulfillment",
            RuntimeOrchestrationArtifactId = artifactId,
            TriggerIntakeId = Id.New(),
            CorrelationId = "shared-trace",
            ExecutionKey = "order.fulfillment::shared-trace::intake",
            Status = OrchestrationInstanceStatus.Created,
            StartedOnUtc = DateTime.UtcNow.AddMinutes(-1),
            LastUpdatedOnUtc = DateTime.UtcNow,
            SnapshotPayload = JsonNode.Parse("""{"orderId":"shared-trace"}""")
        };
        var artifact = CreateArtifact(artifactId, """
        {
          "Key": "order.fulfillment",
          "Version": { "Major": 1, "Minor": 0, "Patch": 0 },
          "StageDefinitions": [
            {
              "Key": "reserve-inventory",
              "Order": 1,
              "TaskDefinitions": [
                {
                  "Key": "reserve-stock",
                  "Order": 1,
                  "Kind": 0,
                  "DispatchType": 2,
                  "Configuration": { "Topic": "inventory.reserve", "Version": "1.0.0" },
                  "IsEnabled": true
                }
              ]
            }
          ]
        }
        """);
        var intake = new TriggerIntake
        {
            Id = instance.TriggerIntakeId,
            TriggerType = TriggerType.Event,
            TriggerKey = instance.OrchestrationDefinitionKey,
            EnvironmentKey = instance.EnvironmentKey,
            CorrelationId = instance.CorrelationId,
            RawPayload = instance.SnapshotPayload!.DeepClone(),
            NormalizedPayload = instance.SnapshotPayload.DeepClone(),
            Status = TriggerIntakeStatus.PromotedToRuntime,
            PersistenceLevel = "Primary",
            BufferLocation = "InMemory",
            ResolvedArtifactId = artifact.Id,
            PromotedInstanceId = instance.Id,
            ReceivedOnUtc = DateTime.UtcNow.AddMinutes(-1)
        };
        store.Instances[instance.Id] = instance;
        store.Artifacts[artifact.Id] = artifact;
        var dispatcher = new RecordingTaskDispatcher();
        var engine = CreateEngine(
            store,
            new SingleItemIntakeBuffer(),
            new InlineTriggerPromoter(new TriggerPromotionResult { Intake = intake, Instance = instance, Artifact = artifact }),
            new RecordingTaskDispatcherResolver(dispatcher));

        var result = await engine.ProcessNext();

        Assert.True(result.Succeeded);
        var task = Assert.Single(store.Tasks.Values);
        Assert.Equal($"shared-trace:{instance.Id}:reserve-stock", task.CorrelationId);
        Assert.Equal(task.CorrelationId, Assert.Single(store.Dispatches.Values).CorrelationId);
        Assert.Equal(task.CorrelationId, Assert.Single(dispatcher.Requests).CorrelationId);
    }

    [Fact]
    public async Task ProcessNext_PersistsWaitingCorrelationBeforeDispatchingAwaitedTask()
    {
        var store = new RuntimeStore();
        var artifactId = Id.New();
        var artifact = CreateArtifact(artifactId, """
        {
          "Key": "order.fulfillment",
          "Version": { "Major": 1, "Minor": 0, "Patch": 0 },
          "StageDefinitions": [
            {
              "Key": "reserve-inventory",
              "Order": 1,
              "TaskDefinitions": [
                {
                  "Key": "reserve-stock",
                  "Order": 1,
                  "Kind": 0,
                  "DispatchType": 2,
                  "Configuration": { "Topic": "inventory.reserve", "Version": "1.0.0" },
                  "IsEnabled": true
                }
              ]
            }
          ]
        }
        """);
        var instance = new OrchestrationInstance
        {
            Id = Id.New(),
            EnvironmentKey = "local",
            OrchestrationDefinitionKey = "order.fulfillment",
            RuntimeOrchestrationArtifactId = artifactId,
            TriggerIntakeId = Id.New(),
            CorrelationId = "fast-response",
            ExecutionKey = "order.fulfillment::fast-response",
            Status = OrchestrationInstanceStatus.Created,
            StartedOnUtc = DateTime.UtcNow.AddMinutes(-1),
            LastUpdatedOnUtc = DateTime.UtcNow,
            SnapshotPayload = JsonNode.Parse("""{"orderId":"fast-response"}""")
        };
        var intake = new TriggerIntake
        {
            Id = Id.New(),
            TriggerType = TriggerType.Event,
            TriggerKey = instance.OrchestrationDefinitionKey,
            EnvironmentKey = instance.EnvironmentKey,
            CorrelationId = instance.CorrelationId,
            RawPayload = instance.SnapshotPayload!.DeepClone(),
            NormalizedPayload = instance.SnapshotPayload.DeepClone(),
            Status = TriggerIntakeStatus.PromotedToRuntime,
            PersistenceLevel = "Primary",
            BufferLocation = "InMemory",
            ResolvedArtifactId = artifact.Id,
            PromotedInstanceId = instance.Id,
            ReceivedOnUtc = DateTime.UtcNow.AddMinutes(-1)
        };
        store.Instances[instance.Id] = instance;
        store.Artifacts[artifact.Id] = artifact;

        var dispatcher = new RecordingTaskDispatcher(request =>
        {
            var task = Assert.Single(store.Tasks.Values);
            var attempt = Assert.Single(store.Attempts.Values);
            Assert.Equal(TaskExecutionStatus.WaitingResponse, task.Status);
            Assert.Equal(TaskExecutionStatus.WaitingResponse, attempt.Status);
            Assert.Equal(request.DispatchId, attempt.DispatchId?.ToString());
            Assert.Equal(task.CorrelationId, request.CorrelationId);
        });
        var engine = CreateEngine(
            store,
            new SingleItemIntakeBuffer(),
            new InlineTriggerPromoter(new TriggerPromotionResult { Intake = intake, Instance = instance, Artifact = artifact }),
            new RecordingTaskDispatcherResolver(dispatcher));

        await engine.ProcessNext();

        Assert.Single(dispatcher.Requests);
    }

    [Fact]
    public async Task ProcessNext_WhenAwaitedDispatchFailsWithContinuePolicy_CompletesInstanceWithoutWaiting()
    {
        var store = new RuntimeStore();
        var artifactId = Id.New();
        var artifact = CreateArtifact(artifactId, """
        {
          "Key": "order.fulfillment",
          "Version": { "Major": 1, "Minor": 0, "Patch": 0 },
          "StageDefinitions": [
            {
              "Key": "optional-failure",
              "Order": 1,
              "TaskDefinitions": [
                {
                  "Key": "optional-failure",
                  "Order": 1,
                  "Kind": 0,
                  "DispatchType": 2,
                  "Configuration": { "Topic": "demo.dispatch-fail", "Version": "1.0.0" },
                  "OnErrorPolicy": "Continue",
                  "IsEnabled": true
                }
              ]
            }
          ]
        }
        """);
        var instance = new OrchestrationInstance
        {
            Id = Id.New(),
            EnvironmentKey = "local",
            OrchestrationDefinitionKey = "order.fulfillment",
            RuntimeOrchestrationArtifactId = artifactId,
            TriggerIntakeId = Id.New(),
            CorrelationId = "continue-after-dispatch-failure",
            ExecutionKey = "order.fulfillment::continue-after-dispatch-failure",
            Status = OrchestrationInstanceStatus.Created,
            StartedOnUtc = DateTime.UtcNow.AddMinutes(-1),
            LastUpdatedOnUtc = DateTime.UtcNow,
            SnapshotPayload = JsonNode.Parse("""{"orderId":"continue-after-dispatch-failure"}""")
        };
        var intake = new TriggerIntake
        {
            Id = instance.TriggerIntakeId,
            TriggerType = TriggerType.Event,
            TriggerKey = instance.OrchestrationDefinitionKey,
            EnvironmentKey = instance.EnvironmentKey,
            CorrelationId = instance.CorrelationId,
            RawPayload = instance.SnapshotPayload!.DeepClone(),
            NormalizedPayload = instance.SnapshotPayload.DeepClone(),
            Status = TriggerIntakeStatus.PromotedToRuntime,
            PersistenceLevel = "Primary",
            BufferLocation = "InMemory",
            ResolvedArtifactId = artifact.Id,
            PromotedInstanceId = instance.Id,
            ReceivedOnUtc = DateTime.UtcNow.AddMinutes(-1)
        };
        store.Instances[instance.Id] = instance;
        store.Artifacts[artifact.Id] = artifact;
        var engine = CreateEngine(
            store,
            new SingleItemIntakeBuffer(),
            new InlineTriggerPromoter(new TriggerPromotionResult { Intake = intake, Instance = instance, Artifact = artifact }),
            new RecordingTaskDispatcherResolver(new FailingTaskDispatcher()));

        var result = await engine.ProcessNext();

        Assert.True(result.Succeeded);
        Assert.Equal(OrchestrationInstanceStatus.Completed, store.Instances[instance.Id].Status);
        Assert.Null(store.Instances[instance.Id].WaitingSinceUtc);
        Assert.Equal(StageExecutionStatus.Completed, Assert.Single(store.Stages.Values).Status);
        var task = Assert.Single(store.Tasks.Values);
        Assert.Equal(TaskExecutionStatus.CompletedWithErrors, task.Status);
        Assert.Null(task.WaitingSinceUtc);
        var attempt = Assert.Single(store.Attempts.Values);
        Assert.Equal(TaskExecutionStatus.Failed, attempt.Status);
        Assert.Null(attempt.WaitingSinceUtc);
    }

    [Fact]
    public async Task ContinueFromResponse_WhenTaskAlreadyCompleted_IgnoresDuplicateWithoutWritingTransitions()
    {
        var store = new RuntimeStore();
        var artifactId = Id.New();
        var instance = new OrchestrationInstance
        {
            Id = Id.New(),
            EnvironmentKey = "local",
            OrchestrationDefinitionKey = "order.fulfillment",
            RuntimeOrchestrationArtifactId = artifactId,
            CorrelationId = "order-1",
            ExecutionKey = "order.fulfillment::order-1",
            Status = OrchestrationInstanceStatus.Completed,
            StartedOnUtc = DateTime.UtcNow.AddMinutes(-1),
            LastUpdatedOnUtc = DateTime.UtcNow,
            CompletedOnUtc = DateTime.UtcNow,
            SnapshotPayload = JsonNode.Parse("""{"orderId":"order-1"}""")
        };
        var stage = new StageExecution
        {
            Id = Id.New(),
            OrchestrationInstanceId = instance.Id,
            StageKey = "reserve-inventory",
            Order = 1,
            Status = StageExecutionStatus.Completed,
            StartedOnUtc = DateTime.UtcNow.AddMinutes(-1),
            CompletedOnUtc = DateTime.UtcNow
        };
        var task = new TaskExecution
        {
            Id = Id.New(),
            OrchestrationInstanceId = instance.Id,
            StageExecutionId = stage.Id,
            TaskKey = "reserve-stock",
            TaskKind = TaskKind.Messaging,
            Status = TaskExecutionStatus.Completed,
            AwaitResponse = true,
            StartedOnUtc = DateTime.UtcNow.AddMinutes(-1),
            CompletedOnUtc = DateTime.UtcNow,
            LastAttemptNumber = 1,
            CorrelationId = "order-1:reserve-stock"
        };
        var attempt = new TaskExecutionAttempt
        {
            Id = Id.New(),
            TaskExecutionId = task.Id,
            AttemptNumber = 1,
            Status = TaskExecutionStatus.Completed,
            StartedOnUtc = DateTime.UtcNow.AddMinutes(-1),
            CompletedOnUtc = DateTime.UtcNow,
            DispatchId = Id.New(),
            RequestPayload = JsonNode.Parse("""{"orderId":"order-1"}"""),
            ResponsePayload = JsonNode.Parse("""{"reserved":true}""")
        };
        var dispatch = new TaskDispatch
        {
            Id = attempt.DispatchId.Value,
            TaskExecutionAttemptId = attempt.Id,
            DispatchType = "Messaging",
            Destination = "inventory.reserve",
            RequestPayload = JsonNode.Parse("""{"orderId":"order-1"}"""),
            DispatchStatus = "Dispatched",
            CommandId = Id.New().ToString(),
            CorrelationId = task.CorrelationId,
            SentOnUtc = DateTime.UtcNow.AddSeconds(-30),
            AcknowledgedOnUtc = DateTime.UtcNow.AddSeconds(-30)
        };

        store.Instances[instance.Id] = instance;
        store.Stages[stage.Id] = stage;
        store.Tasks[task.Id] = task;
        store.Attempts[attempt.Id] = attempt;
        store.Dispatches[dispatch.Id] = dispatch;

        var engine = CreateEngine(store);
        var result = await engine.ContinueFromResponse(new RuntimeMessageResponseCommand
        {
            OrchestrationInstanceId = instance.Id.ToString(),
            TaskExecutionId = task.Id.ToString(),
            DispatchId = attempt.DispatchId.Value.ToString(),
            CorrelationId = task.CorrelationId,
            Payload = JsonNode.Parse("""{"reserved":true}""")
        });

        Assert.True(result.Succeeded);
        Assert.Equal("Completed", result.Status);
        Assert.Contains("ignored", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(store.Transitions);
        Assert.Single(store.Stages);
        Assert.Equal(TaskExecutionStatus.Completed, store.Tasks[task.Id].Status);
    }

    [Fact]
    public async Task ContinueFromResponse_AcquiresAndReleasesInstanceMutationLease()
    {
        var trace = CreateWaitingTrace();
        var engine = CreateEngine(trace.Store);

        var result = await engine.ContinueFromResponse(new RuntimeMessageResponseCommand
        {
            OrchestrationInstanceId = trace.Instance.Id.ToString(),
            TaskExecutionId = trace.Task.Id.ToString(),
            DispatchId = trace.Dispatch.Id.ToString(),
            CorrelationId = trace.Task.CorrelationId,
            Payload = JsonNode.Parse("""{"reserved":true}""")
        });

        Assert.True(result.Succeeded);
        Assert.Equal(1, trace.Store.LeasesAcquired);
        Assert.Equal(1, trace.Store.LeasesReleased);
        Assert.Null(trace.Store.Instances[trace.Instance.Id].ActiveLeaseId);
        Assert.Null(trace.Store.Instances[trace.Instance.Id].ActiveLeaseExpiresOnUtc);
    }

    [Fact]
    public async Task ContinueFromResponse_WhenDispatchNoLongerExists_IgnoresOrphanResponse()
    {
        var trace = CreateWaitingTrace();
        trace.Store.Dispatches.Remove(trace.Dispatch.Id);
        var engine = CreateEngine(trace.Store);

        var result = await engine.ContinueFromResponse(new RuntimeMessageResponseCommand
        {
            OrchestrationInstanceId = trace.Instance.Id.ToString(),
            TaskExecutionId = trace.Task.Id.ToString(),
            DispatchId = trace.Dispatch.Id.ToString(),
            CorrelationId = trace.Task.CorrelationId,
            Payload = JsonNode.Parse("""{"reserved":true}""")
        });

        Assert.True(result.Succeeded);
        Assert.Contains("ignored", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(trace.Store.Transitions);
        Assert.Equal(TaskExecutionStatus.WaitingResponse, trace.Store.Tasks[trace.Task.Id].Status);
    }

    [Fact]
    public async Task ContinueFromResponse_WhenTaskBelongsToAnotherInstance_RejectsTraceWithoutWritingTransitions()
    {
        var trace = CreateWaitingTrace();
        var foreignInstance = new OrchestrationInstance
        {
            Id = Id.New(),
            EnvironmentKey = trace.Instance.EnvironmentKey,
            OrchestrationDefinitionKey = trace.Instance.OrchestrationDefinitionKey,
            RuntimeOrchestrationArtifactId = trace.Instance.RuntimeOrchestrationArtifactId,
            CorrelationId = "order-2",
            ExecutionKey = "order.fulfillment::order-2",
            Status = OrchestrationInstanceStatus.Waiting,
            StartedOnUtc = DateTime.UtcNow.AddMinutes(-1),
            LastUpdatedOnUtc = DateTime.UtcNow,
            WaitingSinceUtc = DateTime.UtcNow
        };
        trace.Store.Instances[foreignInstance.Id] = foreignInstance;

        var engine = CreateEngine(trace.Store);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => engine.ContinueFromResponse(new RuntimeMessageResponseCommand
        {
            OrchestrationInstanceId = foreignInstance.Id.ToString(),
            TaskExecutionId = trace.Task.Id.ToString(),
            DispatchId = trace.Dispatch.Id.ToString(),
            CorrelationId = trace.Task.CorrelationId,
            Payload = JsonNode.Parse("""{"reserved":true}""")
        }));

        Assert.Contains("does not belong to orchestration instance", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(trace.Store.Transitions);
        Assert.Equal(TaskExecutionStatus.WaitingResponse, trace.Store.Tasks[trace.Task.Id].Status);
    }

    [Fact]
    public async Task ContinueFromResponse_WhenCorrelationDoesNotMatchTask_RejectsTraceWithoutWritingTransitions()
    {
        var trace = CreateWaitingTrace();
        var engine = CreateEngine(trace.Store);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => engine.ContinueFromResponse(new RuntimeMessageResponseCommand
        {
            OrchestrationInstanceId = trace.Instance.Id.ToString(),
            TaskExecutionId = trace.Task.Id.ToString(),
            DispatchId = trace.Dispatch.Id.ToString(),
            CorrelationId = "order-1:other-task",
            Payload = JsonNode.Parse("""{"reserved":true}""")
        }));

        Assert.Contains("does not match task correlation", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(trace.Store.Transitions);
        Assert.Equal(TaskExecutionStatus.WaitingResponse, trace.Store.Tasks[trace.Task.Id].Status);
    }

    [Fact]
    public async Task ContinueFromResponse_WhenCorrelationDoesNotMatchDispatch_RejectsTraceWithoutWritingTransitions()
    {
        var trace = CreateWaitingTrace();
        trace.Store.Dispatches[trace.Dispatch.Id].CorrelationId = "order-1:other-dispatch";

        var engine = CreateEngine(trace.Store);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => engine.ContinueFromResponse(new RuntimeMessageResponseCommand
        {
            OrchestrationInstanceId = trace.Instance.Id.ToString(),
            TaskExecutionId = trace.Task.Id.ToString(),
            DispatchId = trace.Dispatch.Id.ToString(),
            CorrelationId = trace.Task.CorrelationId,
            Payload = JsonNode.Parse("""{"reserved":true}""")
        }));

        Assert.Contains("does not match dispatch correlation", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(trace.Store.Transitions);
        Assert.Equal(TaskExecutionStatus.WaitingResponse, trace.Store.Tasks[trace.Task.Id].Status);
    }

    [Fact]
    public async Task ContinueFromResponse_WhenDispatchBelongsToAnotherAttempt_RejectsTraceWithoutWritingTransitions()
    {
        var trace = CreateWaitingTrace();
        trace.Store.Dispatches[trace.Dispatch.Id].TaskExecutionAttemptId = Id.New();

        var engine = CreateEngine(trace.Store);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => engine.ContinueFromResponse(new RuntimeMessageResponseCommand
        {
            OrchestrationInstanceId = trace.Instance.Id.ToString(),
            TaskExecutionId = trace.Task.Id.ToString(),
            DispatchId = trace.Dispatch.Id.ToString(),
            CorrelationId = trace.Task.CorrelationId,
            Payload = JsonNode.Parse("""{"reserved":true}""")
        }));

        Assert.Contains("does not belong to task attempt", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(trace.Store.Transitions);
        Assert.Equal(TaskExecutionStatus.WaitingResponse, trace.Store.Tasks[trace.Task.Id].Status);
    }

    [Fact]
    public async Task ContinueFromResponse_WhenParallelGroupStillHasWaitingTasks_DoesNotAdvanceStage()
    {
        var trace = CreateParallelWaitingTrace();
        var engine = CreateEngine(trace.Store);

        var result = await engine.ContinueFromResponse(new RuntimeMessageResponseCommand
        {
            OrchestrationInstanceId = trace.Instance.Id.ToString(),
            TaskExecutionId = trace.Task.Id.ToString(),
            DispatchId = trace.Dispatch.Id.ToString(),
            CorrelationId = trace.Task.CorrelationId,
            Payload = JsonNode.Parse("""{"reserved":true}""")
        });

        Assert.True(result.Succeeded);
        Assert.Equal("Waiting", result.Status);
        Assert.Equal(OrchestrationInstanceStatus.Waiting, trace.Store.Instances[trace.Instance.Id].Status);
        Assert.Equal(TaskExecutionStatus.Completed, trace.Store.Tasks[trace.Task.Id].Status);
        Assert.Equal(TaskExecutionStatus.WaitingResponse, trace.Store.Tasks[trace.OtherTask.Id].Status);
        Assert.DoesNotContain(trace.Store.Transitions, x => x.TransitionType == "ParallelGroupCompleted");
        Assert.DoesNotContain(trace.Store.Transitions, x => x.TransitionType == "StageCompleted");
        Assert.DoesNotContain(trace.Store.Transitions, x => x.TransitionType == "InstanceCompleted");
    }

    [Fact]
    public async Task ContinueFromResponse_WhenParallelResponseArrivesWhileInstanceIsRunning_ProcessesWaitingTask()
    {
        var trace = CreateParallelWaitingTrace();
        trace.Instance.Status = OrchestrationInstanceStatus.Running;
        trace.Instance.WaitingSinceUtc = null;
        var engine = CreateEngine(trace.Store);

        var result = await engine.ContinueFromResponse(new RuntimeMessageResponseCommand
        {
            OrchestrationInstanceId = trace.Instance.Id.ToString(),
            TaskExecutionId = trace.Task.Id.ToString(),
            DispatchId = trace.Dispatch.Id.ToString(),
            CorrelationId = trace.Task.CorrelationId,
            Payload = JsonNode.Parse("""{"reserved":true}""")
        });

        Assert.True(result.Succeeded);
        Assert.Equal("Waiting", result.Status);
        Assert.Equal(TaskExecutionStatus.Completed, trace.Store.Tasks[trace.Task.Id].Status);
        Assert.Equal(TaskExecutionStatus.WaitingResponse, trace.Store.Tasks[trace.OtherTask.Id].Status);
        Assert.Contains(trace.Store.Transitions, x => x.TransitionType == "TaskResponseReceived");
        Assert.DoesNotContain(trace.Store.Transitions, x => x.TransitionType == "ParallelGroupCompleted");
    }

    private static RuntimeTraceFixture CreateWaitingTrace()
    {
        var store = new RuntimeStore();
        var instance = new OrchestrationInstance
        {
            Id = Id.New(),
            EnvironmentKey = "local",
            OrchestrationDefinitionKey = "order.fulfillment",
            RuntimeOrchestrationArtifactId = Id.New(),
            CorrelationId = "order-1",
            ExecutionKey = "order.fulfillment::order-1",
            Status = OrchestrationInstanceStatus.Waiting,
            StartedOnUtc = DateTime.UtcNow.AddMinutes(-1),
            LastUpdatedOnUtc = DateTime.UtcNow,
            WaitingSinceUtc = DateTime.UtcNow,
            SnapshotPayload = JsonNode.Parse("""{"orderId":"order-1"}""")
        };
        var stage = new StageExecution
        {
            Id = Id.New(),
            OrchestrationInstanceId = instance.Id,
            StageKey = "reserve-inventory",
            Order = 1,
            Status = StageExecutionStatus.Running,
            StartedOnUtc = DateTime.UtcNow.AddMinutes(-1)
        };
        var task = new TaskExecution
        {
            Id = Id.New(),
            OrchestrationInstanceId = instance.Id,
            StageExecutionId = stage.Id,
            TaskKey = "reserve-stock",
            TaskKind = TaskKind.Messaging,
            Status = TaskExecutionStatus.WaitingResponse,
            AwaitResponse = true,
            StartedOnUtc = DateTime.UtcNow.AddMinutes(-1),
            WaitingSinceUtc = DateTime.UtcNow.AddSeconds(-30),
            LastAttemptNumber = 1,
            CorrelationId = "order-1:reserve-stock"
        };
        var attempt = new TaskExecutionAttempt
        {
            Id = Id.New(),
            TaskExecutionId = task.Id,
            AttemptNumber = 1,
            Status = TaskExecutionStatus.WaitingResponse,
            StartedOnUtc = DateTime.UtcNow.AddMinutes(-1),
            WaitingSinceUtc = DateTime.UtcNow.AddSeconds(-30),
            DispatchId = Id.New(),
            RequestPayload = JsonNode.Parse("""{"orderId":"order-1"}""")
        };
        var dispatch = new TaskDispatch
        {
            Id = attempt.DispatchId.Value,
            TaskExecutionAttemptId = attempt.Id,
            DispatchType = "Messaging",
            Destination = "inventory.reserve",
            RequestPayload = JsonNode.Parse("""{"orderId":"order-1"}"""),
            DispatchStatus = "Dispatched",
            CommandId = Id.New().ToString(),
            CorrelationId = task.CorrelationId,
            SentOnUtc = DateTime.UtcNow.AddSeconds(-30),
            AcknowledgedOnUtc = DateTime.UtcNow.AddSeconds(-30)
        };

        store.Instances[instance.Id] = instance;
        store.Artifacts[instance.RuntimeOrchestrationArtifactId] = CreateArtifact(instance.RuntimeOrchestrationArtifactId, """
        {
          "Key": "order.fulfillment",
          "Version": { "Major": 1, "Minor": 0, "Patch": 0 },
          "StageDefinitions": [
            {
              "Key": "reserve-inventory",
              "Order": 1,
              "TaskDefinitions": [
                {
                  "Key": "reserve-stock",
                  "Order": 1,
                  "Kind": 0,
                  "DispatchType": 2,
                  "Configuration": { "Topic": "inventory.reserve", "Version": "1.0.0" },
                  "IsEnabled": true
                }
              ]
            }
          ]
        }
        """);
        store.Stages[stage.Id] = stage;
        store.Tasks[task.Id] = task;
        store.Attempts[attempt.Id] = attempt;
        store.Dispatches[dispatch.Id] = dispatch;

        return new RuntimeTraceFixture(store, instance, stage, task, attempt, dispatch);
    }

    private static RuntimeParallelTraceFixture CreateParallelWaitingTrace()
    {
        var baseTrace = CreateWaitingTrace();
        var groupId = Id.New();
        baseTrace.Task.ExecutionMode = TaskExecutionMode.Parallel;
        baseTrace.Task.ParallelGroupId = groupId;

        var otherTask = new TaskExecution
        {
            Id = Id.New(),
            OrchestrationInstanceId = baseTrace.Instance.Id,
            StageExecutionId = baseTrace.Stage.Id,
            TaskKey = "reserve-promo",
            TaskKind = TaskKind.Messaging,
            ExecutionMode = TaskExecutionMode.Parallel,
            ParallelGroupId = groupId,
            Status = TaskExecutionStatus.WaitingResponse,
            AwaitResponse = true,
            StartedOnUtc = DateTime.UtcNow.AddMinutes(-1),
            WaitingSinceUtc = DateTime.UtcNow.AddSeconds(-25),
            LastAttemptNumber = 1,
            CorrelationId = "order-1:reserve-promo"
        };
        var otherAttempt = new TaskExecutionAttempt
        {
            Id = Id.New(),
            TaskExecutionId = otherTask.Id,
            AttemptNumber = 1,
            Status = TaskExecutionStatus.WaitingResponse,
            StartedOnUtc = DateTime.UtcNow.AddMinutes(-1),
            WaitingSinceUtc = DateTime.UtcNow.AddSeconds(-25),
            DispatchId = Id.New(),
            RequestPayload = JsonNode.Parse("""{"orderId":"order-1"}""")
        };
        var otherDispatch = new TaskDispatch
        {
            Id = otherAttempt.DispatchId.Value,
            TaskExecutionAttemptId = otherAttempt.Id,
            DispatchType = "Messaging",
            Destination = "inventory.reserve-promo",
            RequestPayload = JsonNode.Parse("""{"orderId":"order-1"}"""),
            DispatchStatus = "Dispatched",
            CommandId = Id.New().ToString(),
            CorrelationId = otherTask.CorrelationId,
            SentOnUtc = DateTime.UtcNow.AddSeconds(-25),
            AcknowledgedOnUtc = DateTime.UtcNow.AddSeconds(-25)
        };

        baseTrace.Store.Artifacts[baseTrace.Instance.RuntimeOrchestrationArtifactId] = CreateArtifact(baseTrace.Instance.RuntimeOrchestrationArtifactId, $$"""
        {
          "Key": "order.fulfillment",
          "Version": { "Major": 1, "Minor": 0, "Patch": 0 },
          "StageDefinitions": [
            {
              "Key": "reserve-inventory",
              "Order": 1,
              "ParallelGroups": [
                { "Id": "{{groupId}}", "JoinPolicy": 0, "MaxParallelAgents": 2 }
              ],
              "TaskDefinitions": [
                {
                  "Key": "reserve-stock",
                  "Order": 1,
                  "Kind": 0,
                  "ExecutionMode": 1,
                  "ParallelGroupId": "{{groupId}}",
                  "DispatchType": 2,
                  "Configuration": { "Topic": "inventory.reserve", "Version": "1.0.0" },
                  "IsEnabled": true
                },
                {
                  "Key": "reserve-promo",
                  "Order": 2,
                  "Kind": 0,
                  "ExecutionMode": 1,
                  "ParallelGroupId": "{{groupId}}",
                  "DispatchType": 2,
                  "Configuration": { "Topic": "inventory.reserve-promo", "Version": "1.0.0" },
                  "IsEnabled": true
                }
              ]
            }
          ]
        }
        """);
        baseTrace.Store.Tasks[otherTask.Id] = otherTask;
        baseTrace.Store.Attempts[otherAttempt.Id] = otherAttempt;
        baseTrace.Store.Dispatches[otherDispatch.Id] = otherDispatch;
        return new RuntimeParallelTraceFixture(baseTrace.Store, baseTrace.Instance, baseTrace.Stage, baseTrace.Task, otherTask, baseTrace.Attempt, baseTrace.Dispatch);
    }

    private static RuntimeOrchestrationArtifact CreateArtifact(Id artifactId, string payload)
        => new()
        {
            Id = artifactId,
            EnvironmentKey = "local",
            OrchestrationDefinitionKey = "order.fulfillment",
            ArtifactType = "orchestration",
            Version = new SemanticVersion(1, 0, 0),
            ArtifactChecksum = new Checksum("checksum"),
            ArtifactPayload = JsonNode.Parse(payload),
            IsActive = true,
            DeployedOnUtc = DateTime.UtcNow.AddMinutes(-5),
            ActivatedOnUtc = DateTime.UtcNow.AddMinutes(-5)
        };

    private sealed record RuntimeTraceFixture(
        RuntimeStore Store,
        OrchestrationInstance Instance,
        StageExecution Stage,
        TaskExecution Task,
        TaskExecutionAttempt Attempt,
        TaskDispatch Dispatch);

    private sealed record RuntimeParallelTraceFixture(
        RuntimeStore Store,
        OrchestrationInstance Instance,
        StageExecution Stage,
        TaskExecution Task,
        TaskExecution OtherTask,
        TaskExecutionAttempt Attempt,
        TaskDispatch Dispatch);

    private static RuntimeEngine CreateEngine(
        RuntimeStore store,
        ITriggerIntakeBuffer? intakeBuffer = null,
        ITriggerPromoter? triggerPromoter = null,
        IRuntimeTaskDispatcherResolver? taskDispatcherResolver = null)
        => new(new RuntimeEngineDependencies
        {
            IntakeBuffer = intakeBuffer ?? new ThrowingIntakeBuffer(),
            TriggerPromoter = triggerPromoter ?? new ThrowingTriggerPromoter(),
            ArtifactRepository = new RuntimeArtifactRepositoryStub(store),
            StageRepository = new StageRepositoryStub(store),
            TaskRepository = new TaskRepositoryStub(store),
            AttemptRepository = new AttemptRepositoryStub(store),
            DispatchRepository = new DispatchRepositoryStub(store),
            CompensationRepository = new CompensationRepositoryStub(store),
            InstanceRepository = new InstanceRepositoryStub(store),
            TimelineRepository = new TransitionRepositoryStub(store),
            TaskDispatcherResolver = taskDispatcherResolver ?? new ThrowingTaskDispatcherResolver(),
            ConditionEvaluator = new RuntimeConditionEvaluator(),
            PayloadTransformer = new RuntimePayloadTransformer(),
            RetryPolicyEvaluator = new RuntimeRetryPolicyEvaluator(),
            TimeoutPolicyEvaluator = new RuntimeTimeoutPolicyEvaluator(),
            ErrorPolicyResolver = new RuntimeErrorPolicyResolver(),
            ReactiveEventPublisher = new NoopRuntimeReactiveEventPublisher()
        });

    private sealed class RuntimeStore
    {
        public Dictionary<Id, OrchestrationInstance> Instances { get; } = new();
        public Dictionary<Id, StageExecution> Stages { get; } = new();
        public Dictionary<Id, TaskExecution> Tasks { get; } = new();
        public Dictionary<Id, TaskExecutionAttempt> Attempts { get; } = new();
        public Dictionary<Id, TaskDispatch> Dispatches { get; } = new();
        public Dictionary<Id, RuntimeOrchestrationArtifact> Artifacts { get; } = new();
        public List<ExecutionTransition> Transitions { get; } = new();
        public List<CompensationExecution> Compensations { get; } = new();
        public int LeasesAcquired { get; set; }
        public int LeasesReleased { get; set; }
    }

    private sealed class InstanceRepositoryStub(RuntimeStore store) : IOrchestrationInstanceRepository
    {
        public Task Create(OrchestrationInstance instance, CancellationToken cancellationToken = default)
        {
            store.Instances[instance.Id] = instance;
            return Task.CompletedTask;
        }

        public Task Update(OrchestrationInstance instance, CancellationToken cancellationToken = default)
        {
            store.Instances[instance.Id] = instance;
            return Task.CompletedTask;
        }

        public Task<OrchestrationInstance> GetById(Id instanceId, CancellationToken cancellationToken = default)
            => Task.FromResult(store.Instances[instanceId]);

        public Task<OrchestrationInstanceLease> TryAcquireLease(
            Id instanceId,
            string leaseId,
            DateTime nowUtc,
            DateTime expiresOnUtc,
            CancellationToken cancellationToken = default)
        {
            var instance = store.Instances[instanceId];
            if (!string.IsNullOrWhiteSpace(instance.ActiveLeaseId) && instance.ActiveLeaseExpiresOnUtc > nowUtc)
                return Task.FromResult<OrchestrationInstanceLease>(null!);

            instance.ActiveLeaseId = leaseId;
            instance.ActiveLeaseExpiresOnUtc = expiresOnUtc;
            store.LeasesAcquired++;
            return Task.FromResult(new OrchestrationInstanceLease(instanceId, leaseId, nowUtc, expiresOnUtc));
        }

        public Task ReleaseLease(Id instanceId, string leaseId, CancellationToken cancellationToken = default)
        {
            var instance = store.Instances[instanceId];
            if (string.Equals(instance.ActiveLeaseId, leaseId, StringComparison.Ordinal))
            {
                instance.ActiveLeaseId = null;
                instance.ActiveLeaseExpiresOnUtc = null;
                store.LeasesReleased++;
            }

            return Task.CompletedTask;
        }

        public Task<IReadOnlyCollection<OrchestrationInstance>> GetRecent(string environmentKey, int take = 50, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<OrchestrationInstance>>(store.Instances.Values.ToArray());

        public Task<RuntimeInstanceSummary> GetSummary(string environmentKey, DateTime recentSinceUtc, CancellationToken cancellationToken = default)
        {
            var instances = store.Instances.Values.Where(x => x.EnvironmentKey == environmentKey).ToArray();
            return Task.FromResult(new RuntimeInstanceSummary(
                instances.Count(x => x.Status is OrchestrationInstanceStatus.Running or OrchestrationInstanceStatus.Waiting),
                instances.Count(x => x.Status == OrchestrationInstanceStatus.Waiting),
                instances.Count(x => x.Status == OrchestrationInstanceStatus.Completed && x.LastUpdatedOnUtc >= recentSinceUtc),
                instances.Count(x => x.Status == OrchestrationInstanceStatus.Failed && x.LastUpdatedOnUtc >= recentSinceUtc),
                recentSinceUtc));
        }
    }

    private sealed class StageRepositoryStub(RuntimeStore store) : IStageExecutionRepository
    {
        public Task Create(StageExecution stageExecution, CancellationToken cancellationToken = default)
        {
            store.Stages[stageExecution.Id] = stageExecution;
            return Task.CompletedTask;
        }

        public Task Update(StageExecution stageExecution, CancellationToken cancellationToken = default)
        {
            store.Stages[stageExecution.Id] = stageExecution;
            return Task.CompletedTask;
        }

        public Task<StageExecution> GetById(Id stageExecutionId, CancellationToken cancellationToken = default)
            => Task.FromResult(store.Stages[stageExecutionId]);

        public Task<StageExecution> GetByInstanceAndKey(Id instanceId, string stageKey, CancellationToken cancellationToken = default)
            => Task.FromResult(store.Stages.Values.FirstOrDefault(x =>
                x.OrchestrationInstanceId == instanceId &&
                string.Equals(x.StageKey, stageKey, StringComparison.OrdinalIgnoreCase))!);

        public Task<IReadOnlyCollection<StageExecution>> GetByInstanceId(Id instanceId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<StageExecution>>(store.Stages.Values.Where(x => x.OrchestrationInstanceId == instanceId).ToArray());
    }

    private sealed class TaskRepositoryStub(RuntimeStore store) : ITaskExecutionRepository
    {
        public Task Create(TaskExecution taskExecution, CancellationToken cancellationToken = default)
        {
            store.Tasks[taskExecution.Id] = taskExecution;
            return Task.CompletedTask;
        }

        public Task Update(TaskExecution taskExecution, CancellationToken cancellationToken = default)
        {
            store.Tasks[taskExecution.Id] = taskExecution;
            return Task.CompletedTask;
        }

        public Task<TaskExecution> GetById(Id taskExecutionId, CancellationToken cancellationToken = default)
            => Task.FromResult(store.Tasks[taskExecutionId]);

        public Task<TaskExecution> GetByCorrelationId(string correlationId, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Duplicate responses must resolve tasks by id.");

        public Task<TaskExecution> GetByStageAndKey(Id stageExecutionId, string taskKey, CancellationToken cancellationToken = default)
            => Task.FromResult(store.Tasks.Values.FirstOrDefault(x =>
                x.StageExecutionId == stageExecutionId &&
                string.Equals(x.TaskKey, taskKey, StringComparison.OrdinalIgnoreCase))!);

        public Task<IReadOnlyCollection<TaskExecution>> GetByInstanceId(Id instanceId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<TaskExecution>>(store.Tasks.Values.Where(x => x.OrchestrationInstanceId == instanceId).ToArray());

        public Task<IReadOnlyCollection<TaskExecution>> GetWaitingResponseOlderThan(DateTime dueBeforeUtc, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<TaskExecution>>(store.Tasks.Values
                .Where(x => x.Status == TaskExecutionStatus.WaitingResponse &&
                            x.WaitingSinceUtc.HasValue &&
                            x.WaitingSinceUtc.Value <= dueBeforeUtc)
                .ToArray());
    }

    private sealed class AttemptRepositoryStub(RuntimeStore store) : ITaskExecutionAttemptRepository
    {
        public Task Create(TaskExecutionAttempt attempt, CancellationToken cancellationToken = default)
        {
            store.Attempts[attempt.Id] = attempt;
            return Task.CompletedTask;
        }

        public Task Update(TaskExecutionAttempt attempt, CancellationToken cancellationToken = default)
        {
            store.Attempts[attempt.Id] = attempt;
            return Task.CompletedTask;
        }

        public Task<TaskExecutionAttempt> GetById(Id attemptId, CancellationToken cancellationToken = default)
            => Task.FromResult(store.Attempts[attemptId]);

        public Task<IReadOnlyCollection<TaskExecutionAttempt>> GetByTaskExecutionId(Id taskExecutionId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<TaskExecutionAttempt>>(store.Attempts.Values.Where(x => x.TaskExecutionId == taskExecutionId).ToArray());

        public Task<IReadOnlyCollection<TaskExecutionAttempt>> GetWaitingResponseOlderThan(DateTime dueBeforeUtc, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<TaskExecutionAttempt>>(store.Attempts.Values
                .Where(x => x.Status == TaskExecutionStatus.WaitingResponse &&
                            x.WaitingSinceUtc.HasValue &&
                            x.WaitingSinceUtc.Value <= dueBeforeUtc)
                .ToArray());
    }

    private sealed class DispatchRepositoryStub(RuntimeStore store) : ITaskDispatchRepository
    {
        public Task Create(TaskDispatch dispatch, CancellationToken cancellationToken = default)
        {
            store.Dispatches[dispatch.Id] = dispatch;
            return Task.CompletedTask;
        }

        public Task Update(TaskDispatch dispatch, CancellationToken cancellationToken = default)
        {
            store.Dispatches[dispatch.Id] = dispatch;
            return Task.CompletedTask;
        }

        public Task<TaskDispatch> GetById(Id dispatchId, CancellationToken cancellationToken = default)
            => Task.FromResult(store.Dispatches[dispatchId]);

        public Task<TaskDispatch> TryGetById(Id dispatchId, CancellationToken cancellationToken = default)
            => Task.FromResult(store.Dispatches.TryGetValue(dispatchId, out var dispatch) ? dispatch : null!);

        public Task<TaskDispatch> GetByCommandId(string commandId, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Duplicate responses must not resolve dispatches by command id.");

        public Task<TaskDispatch> GetByAttemptId(Id taskExecutionAttemptId, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Duplicate responses must resolve dispatches by id.");
    }

    private sealed class CompensationRepositoryStub(RuntimeStore store) : ICompensationExecutionRepository
    {
        public Task Create(CompensationExecution compensationExecution, CancellationToken cancellationToken = default)
        {
            store.Compensations.Add(compensationExecution);
            return Task.CompletedTask;
        }

        public Task Update(CompensationExecution compensationExecution, CancellationToken cancellationToken = default)
        {
            var index = store.Compensations.FindIndex(x => x.Id == compensationExecution.Id);
            if (index >= 0)
                store.Compensations[index] = compensationExecution;

            return Task.CompletedTask;
        }

        public Task<IReadOnlyCollection<CompensationExecution>> GetByInstanceId(Id instanceId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<CompensationExecution>>(store.Compensations.Where(x => x.OrchestrationInstanceId == instanceId).ToArray());

        public Task<IReadOnlyCollection<CompensationExecution>> GetPending(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<CompensationExecution>>(store.Compensations.Where(x => x.Status == "Pending").ToArray());
    }

    private sealed class TransitionRepositoryStub(RuntimeStore store) : IExecutionTransitionRepository
    {
        public Task Create(ExecutionTransition transition, CancellationToken cancellationToken = default)
        {
            store.Transitions.Add(transition);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyCollection<ExecutionTransition>> GetByInstanceId(Id instanceId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<ExecutionTransition>>(store.Transitions.Where(x => x.OrchestrationInstanceId == instanceId).ToArray());

        public Task<IReadOnlyCollection<ExecutionTransition>> GetRecent(string environmentKey, int take = 250, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<ExecutionTransition>>(store.Transitions.Take(take).ToArray());

        public Task<IReadOnlyCollection<RuntimeTrafficPoint>> GetTraffic(string environmentKey, DateTime sinceUtc, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<RuntimeTrafficPoint>>(
                store.Transitions
                    .Where(x => x.OccurredOnUtc >= sinceUtc)
                    .GroupBy(x => new DateTime(x.OccurredOnUtc.Year, x.OccurredOnUtc.Month, x.OccurredOnUtc.Day, x.OccurredOnUtc.Hour, x.OccurredOnUtc.Minute, 0, DateTimeKind.Utc))
                    .Select(x => new RuntimeTrafficPoint(
                        x.Key,
                        x.Count(item => item.TransitionType == "InstanceStarted"),
                        x.Count(item => item.TransitionType == "InstanceCompleted"),
                        x.Count(item => item.TransitionType == "InstanceFailed")))
                    .ToArray());
    }

    private sealed class RuntimeArtifactRepositoryStub(RuntimeStore store) : IRuntimeArtifactRepository
    {
        public Task Upsert(RuntimeOrchestrationArtifact artifact, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task DeactivateActiveArtifacts(string environmentKey, string orchestrationDefinitionKey, Id exceptArtifactId, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<RuntimeOrchestrationArtifact> GetById(Id artifactId, CancellationToken cancellationToken = default)
            => Task.FromResult(store.Artifacts[artifactId]);

        public Task<RuntimeOrchestrationArtifact> GetByVersion(string environmentKey, string orchestrationDefinitionKey, SemanticVersion version, CancellationToken cancellationToken = default)
            => Task.FromResult<RuntimeOrchestrationArtifact>(store.Artifacts.Values.FirstOrDefault(x => x.EnvironmentKey == environmentKey && x.OrchestrationDefinitionKey == orchestrationDefinitionKey && x.Version.ToString() == version.ToString())!);

        public Task<IReadOnlyCollection<RuntimeOrchestrationArtifact>> GetAll(string environmentKey, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<RuntimeOrchestrationArtifact>>(Array.Empty<RuntimeOrchestrationArtifact>());

        public Task<RuntimeOrchestrationArtifact> GetActive(string environmentKey, string orchestrationDefinitionKey, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }

    private sealed class ThrowingTaskDispatcherResolver : IRuntimeTaskDispatcherResolver
    {
        public IRuntimeTaskDispatcher Resolve(string taskKind)
            => throw new InvalidOperationException("Duplicate responses must not dispatch tasks.");
    }

    private sealed class RecordingTaskDispatcherResolver(IRuntimeTaskDispatcher dispatcher) : IRuntimeTaskDispatcherResolver
    {
        public IRuntimeTaskDispatcher Resolve(string taskKind) => dispatcher;
    }

    private sealed class RecordingTaskDispatcher : IRuntimeTaskDispatcher
    {
        private readonly Action<RuntimeTaskDispatchRequest>? _onDispatch;

        public RecordingTaskDispatcher(Action<RuntimeTaskDispatchRequest>? onDispatch = null)
        {
            _onDispatch = onDispatch;
        }

        public List<RuntimeTaskDispatchRequest> Requests { get; } = new();

        public bool CanDispatch(string taskKind) => string.Equals(taskKind, "Messaging", StringComparison.OrdinalIgnoreCase);

        public Task<RuntimeTaskDispatchResult> Dispatch(RuntimeTaskDispatchRequest request, CancellationToken cancellationToken = default)
        {
            _onDispatch?.Invoke(request);
            Requests.Add(request);
            return Task.FromResult(new RuntimeTaskDispatchResult
            {
                Succeeded = true,
                Status = "Dispatched",
                ExternalReference = "test"
            });
        }
    }

    private sealed class FailingTaskDispatcher : IRuntimeTaskDispatcher
    {
        public bool CanDispatch(string taskKind) => string.Equals(taskKind, "Messaging", StringComparison.OrdinalIgnoreCase);

        public Task<RuntimeTaskDispatchResult> Dispatch(RuntimeTaskDispatchRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(new RuntimeTaskDispatchResult
            {
                Succeeded = false,
                Status = "Failed",
                FailureReason = "Demo forced dispatch failure."
            });
    }

    private sealed class ThrowingTriggerPromoter : ITriggerPromoter
    {
        public Task<TriggerPromotionResult> Promote(TriggerIntakeBufferItem item, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }

    private sealed class InlineTriggerPromoter(TriggerPromotionResult result) : ITriggerPromoter
    {
        public Task<TriggerPromotionResult> Promote(TriggerIntakeBufferItem item, CancellationToken cancellationToken = default)
            => Task.FromResult(result);
    }

    private sealed class ThrowingIntakeBuffer : ITriggerIntakeBuffer
    {
        public Task<TriggerIntakeBufferResult> Enqueue(TriggerIntakeBufferItem item, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<TriggerIntakeBufferLease> TryDequeue(CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<TriggerIntakeBufferItem> Peek(CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<TriggerIntakeBufferResult> MarkCompleted(Id bufferItemId, string leaseId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<TriggerIntakeBufferResult> MarkFailed(Id bufferItemId, string leaseId, string errorMessage, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }

    private sealed class SingleItemIntakeBuffer : ITriggerIntakeBuffer
    {
        private readonly TriggerIntakeBufferItem _item = new()
        {
            BufferItemId = Id.New(),
            TriggerType = TriggerType.Event,
            TriggerKey = "order.fulfillment",
            ArtifactVersion = "1.0.0",
            EnvironmentKey = "local",
            CorrelationId = "shared-trace",
            IdempotencyKey = "shared-trace-a",
            PayloadJson = """{"orderId":"shared-trace"}""",
            ReceivedOnUtc = DateTime.UtcNow
        };

        private bool _leased;

        public Task<TriggerIntakeBufferResult> Enqueue(TriggerIntakeBufferItem item, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<TriggerIntakeBufferLease> TryDequeue(CancellationToken cancellationToken = default)
        {
            if (_leased)
                return Task.FromResult<TriggerIntakeBufferLease>(null!);

            _leased = true;
            return Task.FromResult(new TriggerIntakeBufferLease(_item, "lease", DateTime.UtcNow));
        }

        public Task<TriggerIntakeBufferItem> Peek(CancellationToken cancellationToken = default)
            => Task.FromResult(_leased ? null! : _item);

        public Task<TriggerIntakeBufferResult> MarkCompleted(Id bufferItemId, string leaseId, CancellationToken cancellationToken = default)
            => Task.FromResult(TriggerIntakeBufferResult.Accept(bufferItemId));

        public Task<TriggerIntakeBufferResult> MarkFailed(Id bufferItemId, string leaseId, string errorMessage, CancellationToken cancellationToken = default)
            => Task.FromResult(TriggerIntakeBufferResult.Reject(errorMessage));
    }
}
