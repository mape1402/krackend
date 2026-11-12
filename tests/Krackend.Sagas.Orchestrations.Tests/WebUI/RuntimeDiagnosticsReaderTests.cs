using System.Text.Json;
using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Artifacts;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Diagnostics;
using System.Reflection;

namespace Krackend.Sagas.Orchestrations.Tests.WebUI;

public sealed class RuntimeDiagnosticsReaderTests
{
    [Fact]
    public void InstanceRowModelBuildsLabelsWithAndWithoutVersion()
    {
        var started = DateTime.UtcNow.AddMinutes(-10);
        var versioned = new InstanceRowModel(
            "instance-1",
            "orders",
            "1.0.0",
            "corr-1",
            "saga-1",
            "execution-1",
            "Running",
            "status-running",
            "stage",
            "task",
            started,
            DateTime.UtcNow,
            null,
            null,
            null,
            string.Empty);
        var unversioned = versioned with { OrchestrationVersion = " " };

        Assert.Equal("orders v1.0.0", versioned.OrchestrationLabel);
        Assert.Equal("orders", unversioned.OrchestrationLabel);
    }

    [Fact]
    public void InstanceCursorRetainsTheLastExecutedLocationWhenCurrentWorkClears()
    {
        var instance = new OrchestrationInstance
        {
            OrchestrationDefinitionKey = "sales.sale.created",
            CorrelationId = "corr-last-execution",
            SagaId = "saga-last-execution",
            ExecutionKey = "sales.sale.created:last-execution",
            CurrentStageKey = "deliveries",
            CurrentTaskKey = "schedule-delivery"
        };

        instance.CaptureLastExecutionCursor();
        instance.CurrentStageKey = string.Empty;
        instance.CurrentTaskKey = string.Empty;
        instance.CaptureLastExecutionCursor();

        Assert.Equal("deliveries", instance.LastExecutedStageKey);
        Assert.Equal("schedule-delivery", instance.LastExecutedTaskKey);
    }

    [Fact]
    public void ReactiveProgressUsesPersistedCountsWithoutLoadingArtifact()
    {
        var store = RuntimeDiagnosticsStore.Create();
        store.UseArtifactWithPendingConfiguredItems();
        store.Instance.Status = OrchestrationInstanceStatus.Running;
        store.Instance.CurrentStageKey = "reserve-stock";
        store.Instance.CurrentTaskKey = "notify-customer";
        store.Instance.CompletedTaskCount = 1;
        store.Instance.TotalTaskCount = 3;

        var active = RuntimeReactiveProgressCalculator.TryCalculate(store.Instance, store.Artifact);

        Assert.NotNull(active);
        Assert.Equal(1, active.Value.CompletedTaskCount);
        Assert.Equal(3, active.Value.TotalTaskCount);

        store.Instance.Status = OrchestrationInstanceStatus.Completed;
        store.Instance.CompletedTaskCount = 2;
        var completed = RuntimeReactiveProgressCalculator.TryCalculate(store.Instance, store.Artifact);

        Assert.Equal(2, completed!.Value.CompletedTaskCount);
        Assert.Equal(3, completed.Value.TotalTaskCount);
    }

    [Fact]
    public void ReactiveProgressReturnsNullWhenProjectionIsUnknownOrInvalid()
    {
        var store = RuntimeDiagnosticsStore.Create();
        store.Artifact.ArtifactPayload = JsonNode.Parse("""{"version":"not-a-semver"}""")!;

        Assert.Null(RuntimeReactiveProgressCalculator.TryCalculate(store.Instance, store.Artifact));
        Assert.Null(RuntimeReactiveProgressCalculator.TryCalculate(null!, store.Artifact));
        Assert.Null(RuntimeReactiveProgressCalculator.TryCalculate(store.Instance, null!));
        store.Instance.CompletedTaskCount = 4;
        store.Instance.TotalTaskCount = 3;
        Assert.Null(RuntimeReactiveProgressCalculator.TryCalculate(store.Instance, store.Artifact));
    }

    [Fact]
    public async Task GetDetail_ReturnsCompleteTraceWithFunctionalAndTechnicalTimelines()
    {
        var store = RuntimeDiagnosticsStore.Create();
        var reader = CreateReader(store);

        var detail = await reader.GetDetail(store.Instance.Id.ToString());

        Assert.Equal(store.Instance.Id.ToString(), detail.Instance.Id);
        Assert.Equal("corr-123", detail.Instance.CorrelationId);
        Assert.Equal("saga-123", detail.Instance.SagaId);
        Assert.Equal("orders", detail.Instance.OrchestrationDefinitionKey);
        Assert.Equal("reserve-stock", Assert.Single(detail.Stages).StageKey);
        Assert.Equal("reserve", Assert.Single(detail.Tasks).TaskKey);
        Assert.Equal(store.Task.Id.ToString(), Assert.Single(detail.Compensations).SourceTaskExecutionId);
        Assert.Equal("reserve", Assert.Single(detail.Compensations).SourceTaskKey);
        Assert.Equal("release-stock", Assert.Single(detail.Compensations).CompensationTaskKey);

        var task = Assert.Single(detail.Tasks);
        Assert.Equal("saga-123", task.SagaId);
        Assert.Equal("StopAndCompensate", task.OnErrorPolicy);
        Assert.Equal(store.Dispatch.Id.ToString(), Assert.Single(task.Attempts).DispatchId);
        Assert.Equal("orders.reserve", Assert.Single(task.Attempts).Dispatch.Destination);

        Assert.Collection(
            detail.FunctionalTimeline,
            item =>
            {
                Assert.Equal("TaskCompleted", item.TransitionType);
                Assert.Equal("reserve", item.TaskKey);
                Assert.Equal("reserve-stock", item.StageKey);
            });

        Assert.Collection(
            detail.TechnicalTimeline,
            item =>
            {
                Assert.Equal("TaskDispatchCreated", item.TransitionType);
                Assert.Equal("reserve", item.TaskKey);
            });

        Assert.Collection(
            detail.Transitions,
            first => Assert.Equal(store.OlderTransition.Id.ToString(), first.Id),
            second => Assert.Equal(store.NewerTransition.Id.ToString(), second.Id));
    }

    [Fact]
    public async Task DetailSectionLoadsOnlyItsRelevantHistory()
    {
        var store = RuntimeDiagnosticsStore.Create();
        var reader = CreateReader(store);
        var execution = await reader.GetDetailForSection(store.Instance.Id.ToString(), "execution");
        var activity = await reader.GetDetailForSection(store.Instance.Id.ToString(), "activity");
        var compensations = await reader.GetDetailForSection(store.Instance.Id.ToString(), "compensations");
        Assert.Empty(execution.Transitions);
        Assert.Empty(execution.Compensations);
        Assert.NotEmpty(activity.Transitions);
        Assert.Empty(activity.Compensations);
        Assert.Empty(compensations.Transitions);
        Assert.NotEmpty(compensations.Compensations);
    }

    [Fact]
    public async Task DetailSectionLoadsDispatchAndPayloadOnlyForSelectedAttempt()
    {
        var store = RuntimeDiagnosticsStore.Create();
        store.Attempt.RequestPayload = JsonNode.Parse("""{"id":9007199254740993}""");
        var attempts = new BatchAttemptRepository(store, [store.Attempt]);
        var dispatches = new BatchDispatchRepository(store, [store.Dispatch], []);
        var reader = CreateReader(store, attempts, dispatches);

        var taskView = await reader.GetDetailForSection(store.Instance.Id.ToString(), "task");
        var summaryAttempt = Assert.Single(Assert.Single(taskView.Tasks).Attempts);
        Assert.Null(summaryAttempt.Dispatch);
        Assert.Empty(summaryAttempt.RequestPayload);
        Assert.Equal(0, dispatches.IdCalls);
        Assert.Equal(0, dispatches.AttemptCalls);

        var selection = new RuntimeDetailSelection(store.Stage.StageKey, store.Task.TaskKey, store.Stage.Id.ToString(), store.Task.Id.ToString(), store.Attempt.AttemptNumber);
        var attemptView = await reader.GetDetailForSection(store.Instance.Id.ToString(), "attempt", selection: selection);
        var selected = Assert.Single(Assert.Single(attemptView.Tasks).Attempts);
        Assert.Contains("9007199254740993", selected.RequestPayload);
        Assert.NotNull(selected.Dispatch);
        Assert.Equal(1, dispatches.IdCalls);
        Assert.Equal(0, dispatches.AttemptCalls);
    }

    [Fact]
    public async Task DetailSectionUsesAttemptSummariesBeforeLoadingSelectedAttempt()
    {
        var store = RuntimeDiagnosticsStore.Create();
        store.Attempt.RequestPayload = JsonNode.Parse("""{"value":"kept"}""");
        var attempts = new SummaryAttemptRepository(store);
        var reader = CreateReader(store, attempts, new BatchDispatchRepository(store, [store.Dispatch], []));
        var taskView = await reader.GetDetailForSection(store.Instance.Id.ToString(), "task");
        Assert.Empty(Assert.Single(Assert.Single(taskView.Tasks).Attempts).RequestPayload);
        Assert.Equal(1, attempts.SummaryCalls);
        Assert.Equal(0, attempts.BatchCalls);
        var selection = new RuntimeDetailSelection(store.Stage.StageKey, store.Task.TaskKey, store.Stage.Id.ToString(), store.Task.Id.ToString(), store.Attempt.AttemptNumber);
        var attemptView = await reader.GetDetailForSection(store.Instance.Id.ToString(), "attempt", selection: selection);
        Assert.Contains("kept", Assert.Single(Assert.Single(attemptView.Tasks).Attempts).RequestPayload);
        Assert.Equal(2, attempts.SummaryCalls);
        Assert.Equal(0, attempts.BatchCalls);
    }

    [Fact]
    public async Task GetSnapshotAndSummary_ReturnTrafficAndVersionedRows()
    {
        var bucket = new DateTime(2026, 8, 12, 12, 1, 0, DateTimeKind.Utc);
        var store = RuntimeDiagnosticsStore.Create();
        store.Instance.OrchestrationVersion = "1.0.0";
        store.Instance.Metadata["TriggerTraceId"] = JsonValue.Create("trace-123");
        store.TrafficPoints = [new RuntimeTrafficPoint(bucket, 2, 3, 4, 5)];
        var reader = CreateReader(store);

        var snapshot = await reader.GetSnapshot();
        var summary = await reader.GetSummary();

        var instance = Assert.Single(snapshot.Instances);
        Assert.Equal("1.0.0", instance.OrchestrationVersion);
        Assert.Equal("trace-123", instance.TraceId);
        Assert.Equal("orders v1.0.0", instance.OrchestrationLabel);
        Assert.Equal("reserve-stock", instance.LastExecutedStageKey);
        Assert.Equal("reserve", instance.LastExecutedTaskKey);
        Assert.Equal(1, snapshot.Summary.CompletedLastMinute);
        Assert.Equal(bucket, Assert.Single(snapshot.Traffic).BucketUtc);
        Assert.Equal(5, Assert.Single(summary.Traffic).Failed);
        Assert.Equal(5, Assert.Single(summary.HourlyTraffic).Failed);
    }

    [Fact]
    public async Task GetDetail_WhenArtifactCannotBeLoaded_ReturnsExecutedTraceWithoutConfiguredVersion()
    {
        var store = RuntimeDiagnosticsStore.Create();
        store.ArtifactLookupFails = true;
        var reader = CreateReader(store);

        var detail = await reader.GetDetail(store.Instance.Id.ToString());

        Assert.Empty(detail.Instance.OrchestrationVersion);
        Assert.True(Assert.Single(detail.Stages).HasExecution);
        Assert.True(Assert.Single(detail.Tasks).HasExecution);
    }

    [Fact]
    public async Task GetDetail_WhenArtifactPayloadIsInvalid_ReturnsExecutedTraceWithoutConfiguredStages()
    {
        var store = RuntimeDiagnosticsStore.Create();
        store.Artifact.ArtifactPayload = JsonNode.Parse("""{"version":"not-a-semver"}""")!;
        var reader = CreateReader(store);

        var detail = await reader.GetDetail(store.Instance.Id.ToString());

        Assert.Single(detail.Stages);
        Assert.Single(detail.Tasks);
        Assert.True(Assert.Single(detail.Tasks).HasExecution);
    }

    [Fact]
    public async Task GetDetail_WhenArtifactStageDefinitionsAreNull_ReturnsExecutedTraceWithoutConfiguredStages()
    {
        var store = RuntimeDiagnosticsStore.Create();
        store.Artifact.ArtifactPayload["stageDefinitions"] = null;
        var reader = CreateReader(store);

        var detail = await reader.GetDetail(store.Instance.Id.ToString());

        Assert.Single(detail.Stages);
        Assert.Single(detail.Tasks);
        Assert.True(Assert.Single(detail.Stages).HasExecution);
    }

    [Fact]
    public async Task GetDetail_WhenArtifactContainsConfiguredItems_AddsPendingStagesAndTasks()
    {
        var store = RuntimeDiagnosticsStore.Create();
        store.UseArtifactWithPendingConfiguredItems();
        var reader = CreateReader(store);

        var detail = await reader.GetDetail(store.Instance.Id.ToString());

        Assert.Collection(
            detail.Stages,
            stage =>
            {
                Assert.Equal("reserve-stock", stage.StageKey);
                Assert.True(stage.HasExecution);
                Assert.Contains(stage.Tasks, task => task.TaskKey == "notify-customer" && !task.HasExecution);
            },
            stage =>
            {
                Assert.Equal("capture-payment", stage.StageKey);
                Assert.False(stage.HasExecution);
                Assert.Equal("Pending", stage.Status);
                Assert.Contains("TaskCount", stage.Metadata);
                Assert.All(stage.Tasks, task => Assert.False(task.HasExecution));
            });
    }

    [Fact]
    public async Task GetDetail_WhenPendingConfiguredStageHasNoParallelGroups_UsesZero()
    {
        var store = RuntimeDiagnosticsStore.Create();
        store.UseArtifactWithPendingConfiguredItems();
        store.Artifact.ArtifactPayload["stageDefinitions"]!.AsArray()[1]!["parallelGroups"] = null;
        var reader = CreateReader(store);

        var detail = await reader.GetDetail(store.Instance.Id.ToString());

        var pendingStage = Assert.Single(detail.Stages.Where(stage => !stage.HasExecution));
        Assert.Equal(0, pendingStage.ParallelGroupCount);
    }

    [Fact]
    public async Task GetDetail_WhenConfiguredStageIsDisabled_OmitsIt()
    {
        var store = RuntimeDiagnosticsStore.Create();
        store.UseArtifactWithPendingConfiguredItems();
        store.Artifact.ArtifactPayload["stageDefinitions"]!.AsArray()[1]!["isEnabled"] = false;
        var reader = CreateReader(store);

        var detail = await reader.GetDetail(store.Instance.Id.ToString());

        Assert.DoesNotContain(detail.Stages, stage => stage.StageKey == "capture-payment");
    }

    [Fact]
    public async Task GetDetail_WhenConfiguredTaskIsDisabled_OmitsItWithoutChangingItsStage()
    {
        var store = RuntimeDiagnosticsStore.Create();
        store.UseArtifactWithPendingConfiguredItems();
        store.Artifact.ArtifactPayload["stageDefinitions"]!.AsArray()[0]!["taskDefinitions"]!.AsArray()[1]!["isEnabled"] = false;
        var reader = CreateReader(store);

        var detail = await reader.GetDetail(store.Instance.Id.ToString());

        var stage = Assert.Single(detail.Stages.Where(candidate => candidate.StageKey == "reserve-stock"));
        Assert.DoesNotContain(stage.Tasks, task => task.TaskKey == "notify-customer");
    }

    [Fact]
    public async Task GetDetail_WhenExecutedEnabledTaskWasSkipped_PreservesIt()
    {
        var store = RuntimeDiagnosticsStore.Create();
        store.UseArtifactWithPendingConfiguredItems();
        store.Task.WasSkipped = true;
        store.Task.SkipReason = "Condition evaluated to false";
        var detail = await CreateReader(store).GetDetail(store.Instance.Id.ToString());
        var task = Assert.Single(detail.Tasks.Where(x => x.TaskKey == store.Task.TaskKey));
        Assert.True(task.WasSkipped);
        Assert.Equal("Condition evaluated to false", task.SkipReason);
    }

    [Fact]
    public async Task GetDetail_WhenExecutedStageOrTaskIsDisabled_OmitsRecordedWork()
    {
        var store = RuntimeDiagnosticsStore.Create();
        store.UseArtifactWithPendingConfiguredItems();
        store.Artifact.ArtifactPayload["stageDefinitions"]!.AsArray()[0]!["taskDefinitions"]!.AsArray()[0]!["isEnabled"] = false;
        var taskDetail = await CreateReader(store).GetDetail(store.Instance.Id.ToString());
        Assert.DoesNotContain(taskDetail.Tasks, task => task.TaskKey == store.Task.TaskKey);
        store.Artifact.ArtifactPayload["stageDefinitions"]!.AsArray()[0]!["isEnabled"] = false;
        var stageDetail = await CreateReader(store).GetDetail(store.Instance.Id.ToString());
        Assert.DoesNotContain(stageDetail.Stages, stage => stage.StageKey == store.Stage.StageKey);
        Assert.DoesNotContain(stageDetail.Tasks, task => task.TaskKey == store.Task.TaskKey);
    }

    [Fact]
    public async Task GetDetail_WhenExecutedTaskIsNotInArtifact_KeepsItAfterConfiguredPendingTasks()
    {
        var store = RuntimeDiagnosticsStore.Create();
        store.Task.TaskKey = "legacy-task";
        var reader = CreateReader(store);

        var detail = await reader.GetDetail(store.Instance.Id.ToString());

        Assert.Contains(detail.Tasks, task => task.TaskKey == "reserve" && !task.HasExecution);
        Assert.Contains(detail.Tasks, task => task.TaskKey == "legacy-task" && task.HasExecution);
    }

    [Fact]
    public async Task GetDetail_WhenAttemptDoesNotReferenceDispatch_LoadsDispatchByAttempt()
    {
        var store = RuntimeDiagnosticsStore.Create();
        store.Attempt.DispatchId = null;
        var reader = CreateReader(store);

        var detail = await reader.GetDetail(store.Instance.Id.ToString());

        Assert.Equal(store.Dispatch.Id.ToString(), Assert.Single(Assert.Single(detail.Tasks).Attempts).Dispatch.Id);
    }

    [Fact]
    public async Task GetDetail_FormatsMetadataDictionaries()
    {
        var store = RuntimeDiagnosticsStore.Create();
        store.Instance.Metadata["tenant"] = JsonValue.Create("north")!;
        store.Task.Metadata["worker"] = JsonValue.Create("inventory")!;
        store.Attempt.Metadata["elapsed"] = JsonValue.Create(123)!;
        store.Dispatch.Metadata["provider"] = JsonValue.Create("pigeon")!;
        store.Compensation.Metadata["reason"] = JsonValue.Create("rollback")!;
        var reader = CreateReader(store);

        var detail = await reader.GetDetail(store.Instance.Id.ToString());

        Assert.Contains("\"tenant\": \"north\"", detail.Metadata);
        Assert.Contains("\"worker\": \"inventory\"", Assert.Single(detail.Tasks).Metadata);
        var attempt = Assert.Single(Assert.Single(detail.Tasks).Attempts);
        Assert.Contains("\"elapsed\": 123", attempt.Metadata);
        Assert.Contains("\"provider\": \"pigeon\"", attempt.Dispatch.Metadata);
        Assert.Contains("\"reason\": \"rollback\"", Assert.Single(detail.Compensations).Metadata);
    }

    [Fact]
    public async Task GetDetail_WhenDispatchMetadataIsEmpty_ReconstructsOrchestrationMetadata()
    {
        var store = RuntimeDiagnosticsStore.Create();
        var propagationMetadata = new OrchestrationPropagationMetadata
        {
            Items =
            {
                [OrchestrationMetadataConstants.TriggerMetadataKey] =
                    JsonNode.Parse("""{"correlationId":"corr-123","traceId":"trace-123"}""")!,
                ["audit.context"] = JsonNode.Parse("""{"requestId":"req-123"}""")!
            }
        };
        store.Instance.Metadata[OrchestrationMetadataConstants.OrchestrationPropagationMetadataKey] =
            JsonSerializer.SerializeToNode(propagationMetadata, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        var reader = CreateReader(store);

        var detail = await reader.GetDetail(store.Instance.Id.ToString());

        var dispatchMetadata = Assert.Single(Assert.Single(detail.Tasks).Attempts).Dispatch.Metadata;
        Assert.Contains(OrchestrationMetadataConstants.OrchestrationMessageMetadataKey, dispatchMetadata);
        Assert.DoesNotContain(OrchestrationMetadataConstants.OrchestrationPropagationMetadataKey, dispatchMetadata);
        Assert.Contains(OrchestrationMetadataConstants.TriggerMetadataKey, dispatchMetadata);
        Assert.Contains("\"traceId\": \"trace-123\"", dispatchMetadata);
        Assert.Contains("\"audit.context\"", dispatchMetadata);
        Assert.Contains("\"requestId\": \"req-123\"", dispatchMetadata);
        Assert.Contains("\"currentStage\": \"reserve-stock\"", dispatchMetadata);
        Assert.Contains("\"attempt\": 1", dispatchMetadata);
    }

    [Fact]
    public async Task GetDetail_WhenDispatchMetadataIsEmptyAndPropagationIsMissing_UsesRuntimeFallbackMetadata()
    {
        var store = RuntimeDiagnosticsStore.Create();
        store.Instance.SagaId = string.Empty;
        store.Instance.Metadata.Clear();
        store.Dispatch.Metadata = null!;
        store.Dispatch.CorrelationId = string.Empty;
        store.Task.CorrelationId = string.Empty;
        var reader = CreateReader(store);

        var detail = await reader.GetDetail(store.Instance.Id.ToString());

        var dispatchMetadata = Assert.Single(Assert.Single(detail.Tasks).Attempts).Dispatch.Metadata;
        Assert.Contains($"\"sagaId\": \"{store.Instance.Id}\"", dispatchMetadata);
        Assert.Contains("\"correlationId\": \"corr-123\"", dispatchMetadata);
        Assert.Contains("\"currentTasks\": [", dispatchMetadata);
        Assert.DoesNotContain(OrchestrationMetadataConstants.OrchestrationPropagationMetadataKey, dispatchMetadata);
    }

    [Fact]
    public async Task GetDetail_WhenPropagationMetadataEntryIsNull_UsesRuntimeFallbackMetadata()
    {
        var store = RuntimeDiagnosticsStore.Create();
        store.Dispatch.Metadata = null!;
        store.Instance.Metadata[OrchestrationMetadataConstants.OrchestrationPropagationMetadataKey] = null!;
        var reader = CreateReader(store);

        var detail = await reader.GetDetail(store.Instance.Id.ToString());

        var dispatchMetadata = Assert.Single(Assert.Single(detail.Tasks).Attempts).Dispatch.Metadata;
        Assert.Contains(OrchestrationMetadataConstants.OrchestrationMessageMetadataKey, dispatchMetadata);
        Assert.DoesNotContain(OrchestrationMetadataConstants.OrchestrationPropagationMetadataKey, dispatchMetadata);
    }

    [Fact]
    public async Task GetDetail_WhenTransitionsAndCompensationsReferenceMissingExecutions_LeavesKeysEmpty()
    {
        var store = RuntimeDiagnosticsStore.Create();
        var missingTaskId = Id.New();
        store.Compensation.SourceTaskExecutionId = missingTaskId;
        store.OlderTransition.StageExecutionId = null;
        store.OlderTransition.TaskExecutionId = null;
        store.OlderTransition.TaskExecutionAttemptId = null;
        var reader = CreateReader(store);

        var detail = await reader.GetDetail(store.Instance.Id.ToString());

        var transition = detail.Transitions.Single(x => x.Id == store.OlderTransition.Id.ToString());
        Assert.Null(transition.StageExecutionId);
        Assert.Null(transition.StageKey);
        Assert.Null(transition.TaskExecutionId);
        Assert.Null(transition.TaskKey);
        Assert.Null(transition.TaskExecutionAttemptId);
        var compensation = Assert.Single(detail.Compensations);
        Assert.Equal(missingTaskId.ToString(), compensation.SourceTaskExecutionId);
        Assert.Null(compensation.SourceTaskKey);
    }

    [Fact]
    public void PropagationMetadataDeserializersHandleFallbackItemsAndInvalidNodes()
    {
        var envelopeMethod = typeof(RuntimeDiagnosticsReader).GetMethod(
            "TryDeserializePropagationEnvelope",
            BindingFlags.Static | BindingFlags.NonPublic)!;
        var itemsMethod = typeof(RuntimeDiagnosticsReader).GetMethod(
            "TryDeserializePropagationItems",
            BindingFlags.Static | BindingFlags.NonPublic)!;
        var malformedEnvelope = JsonNode.Parse("""{"items":[1]}""")!;
        var nullEnvelope = JsonNode.Parse("""{"items":null}""")!;
        var itemDictionary = JsonNode.Parse("""{"audit.context":{"requestId":"req-items"}}""")!;
        var itemDictionaryWithNull = JsonNode.Parse("""{"audit.context":null}""")!;
        var invalidItems = JsonNode.Parse("""[1]""")!;
        var loadPropagationMethod = typeof(RuntimeDiagnosticsReader).GetMethod(
            "LoadPropagationMetadata",
            BindingFlags.Static | BindingFlags.NonPublic)!;
        var instanceWithInvalidPropagation = RuntimeDiagnosticsStore.Create().Instance;
        instanceWithInvalidPropagation.Metadata[OrchestrationMetadataConstants.OrchestrationPropagationMetadataKey] =
            JsonNode.Parse("""[1]""")!;

        var envelope = envelopeMethod.Invoke(null, [malformedEnvelope]);
        var envelopeWithNullItems = envelopeMethod.Invoke(null, [nullEnvelope]);
        var items = (OrchestrationPropagationMetadata)itemsMethod.Invoke(null, [itemDictionary])!;
        var itemsWithNull = (OrchestrationPropagationMetadata)itemsMethod.Invoke(null, [itemDictionaryWithNull])!;
        var invalid = itemsMethod.Invoke(null, [invalidItems]);
        var loadedFromNullInstance = (OrchestrationPropagationMetadata)loadPropagationMethod.Invoke(null, [null])!;
        var loadedFromInvalidNode = (OrchestrationPropagationMetadata)loadPropagationMethod.Invoke(null, [instanceWithInvalidPropagation])!;

        Assert.Null(envelope);
        Assert.Null(envelopeWithNullItems);
        Assert.Equal("req-items", items.Items["audit.context"]!["requestId"]!.GetValue<string>());
        Assert.True(itemsWithNull.Items.ContainsKey("audit.context"));
        Assert.Null(itemsWithNull.Items["audit.context"]);
        Assert.Null(invalid);
        Assert.False(loadedFromNullInstance.HasItems);
        Assert.False(loadedFromInvalidNode.HasItems);
    }

    [Fact]
    public void ArtifactMetadataBuildersHandleOptionalCollectionsAndTaskOptions()
    {
        var stageMethod = typeof(RuntimeDiagnosticsReader).GetMethod(
            "BuildStageArtifactMetadata",
            BindingFlags.Static | BindingFlags.NonPublic)!;
        var taskMethod = typeof(RuntimeDiagnosticsReader).GetMethod(
            "BuildTaskArtifactMetadata",
            BindingFlags.Static | BindingFlags.NonPublic)!;
        var configuration = new MessagingTaskConfigurationArtifact(
            "orders.reserve",
            new SemanticVersion(1, 0, 0),
            null);
        var task = new TaskArtifact(
            Id.New(),
            "reserve",
            "Reserve inventory",
            1,
            string.Empty,
            TaskKind.Messaging,
            TaskExecutionMode.Sequential,
            null,
            null,
            null,
            configuration,
            null,
            null,
            OnErrorPolicy.Stop,
            null,
            TaskDispatchType.FireAndForget,
            false);
        var stage = new StageArtifact(
            Id.New(),
            "reserve-stock",
            "Reserve stock",
            1,
            false,
            null,
            null!,
            null!,
            null!,
            string.Empty);

        var stageMetadata = (JsonNode)stageMethod.Invoke(null, [stage])!;
        var taskMetadata = (JsonNode)taskMethod.Invoke(null, [task])!;

        Assert.Equal(0, stageMetadata["TaskCount"]!.GetValue<int>());
        Assert.Equal(0, stageMetadata["ParallelGroupCount"]!.GetValue<int>());
        Assert.Equal(0, stageMetadata["BranchRuleCount"]!.GetValue<int>());
        Assert.False(stageMetadata["HasExecutionCondition"]!.GetValue<bool>());
        Assert.False(taskMetadata["AwaitResponse"]!.GetValue<bool>());
        Assert.False(taskMetadata["HasExecutionCondition"]!.GetValue<bool>());
        Assert.False(taskMetadata["HasTransformation"]!.GetValue<bool>());
        Assert.False(taskMetadata["HasRetryPolicy"]!.GetValue<bool>());
        Assert.False(taskMetadata["HasTimeoutPolicy"]!.GetValue<bool>());
        Assert.False(taskMetadata["HasCompensation"]!.GetValue<bool>());
    }

    [Theory]
    [InlineData("", typeof(ArgumentException))]
    [InlineData("not-an-ulid", typeof(ArgumentException))]
    public async Task GetDetail_RejectsInvalidInstanceIds(string instanceId, Type exceptionType)
    {
        var reader = CreateReader(RuntimeDiagnosticsStore.Create());

        var exception = await Record.ExceptionAsync(() => reader.GetDetail(instanceId));

        Assert.NotNull(exception);
        Assert.IsType(exceptionType, exception);
    }

    [Theory]
    [InlineData("Created", "od-status-inactive")]
    [InlineData("Pending", "od-status-inactive")]
    [InlineData("Skipped", "od-status-inactive")]
    [InlineData("Stopped", "od-status-inactive")]
    [InlineData("Running", "od-status-running")]
    [InlineData("Retrying", "od-status-warning")]
    [InlineData("CompletedWithErrors", "od-status-warning")]
    [InlineData("DeadLettered", "od-status-warning")]
    [InlineData("Waiting", "od-status-waiting")]
    [InlineData("WaitingResponse", "od-status-waiting")]
    [InlineData("Compensating", "od-status-waiting")]
    [InlineData("Completed", "od-status-active")]
    [InlineData("Compensated", "od-status-active")]
    [InlineData("Failed", "od-status-danger")]
    [InlineData("TimedOut", "od-status-danger")]
    [InlineData("Cancelled", "od-status-danger")]
    [InlineData("Aborted", "od-status-danger")]
    [InlineData("anything-else", "od-status-inactive")]
    public void StatusClass_MapsRuntimeStatuses(string status, string expectedClass)
        => Assert.Equal(expectedClass, RuntimeDiagnosticsReader.StatusClass(status));

    [Fact]
    public async Task DetailUsesBatchesAndPreservesAttemptOrderingAndDispatchLookup()
    {
        var store = RuntimeDiagnosticsStore.Create();
        var task = new TaskExecution { Id = Id.New(), OrchestrationInstanceId = store.Instance.Id,
            StageExecutionId = store.Stage.Id, TaskKey = "other", Status = TaskExecutionStatus.Running };
        store.ExtraTasks = [task];
        var second = new TaskExecutionAttempt { Id = Id.New(), TaskExecutionId = task.Id,
            AttemptNumber = 2, DispatchId = Id.New() };
        var first = new TaskExecutionAttempt { Id = Id.New(), TaskExecutionId = task.Id, AttemptNumber = 1 };
        var explicitDispatch = new TaskDispatch { Id = second.DispatchId.Value, TaskExecutionAttemptId = second.Id,
            DispatchType = "Messaging", DispatchStatus = "Sent", Destination = "explicit" };
        var legacyDispatch = new TaskDispatch { Id = Id.New(), TaskExecutionAttemptId = first.Id,
            DispatchType = "Messaging", DispatchStatus = "Sent", Destination = "legacy" };
        var attempts = new BatchAttemptRepository(store, [second, store.Attempt, first]);
        var dispatches = new BatchDispatchRepository(store, [store.Dispatch, explicitDispatch], [legacyDispatch]);

        var detail = await CreateReader(store, attempts, dispatches).GetDetail(store.Instance.Id.ToString());

        var other = detail.Tasks.Single(x => x.Id == task.Id.ToString());
        Assert.Equal([1, 2], other.Attempts.Select(x => x.AttemptNumber));
        Assert.Equal(["legacy", "explicit"], other.Attempts.Select(x => x.Dispatch.Destination));
        Assert.Equal(1, attempts.BatchCalls);
        Assert.Equal(1, dispatches.IdCalls);
        Assert.Equal(1, dispatches.AttemptCalls);
    }

    [Fact]
    public async Task DetailReportsMissingDispatchWithBatchReader()
    {
        var store = RuntimeDiagnosticsStore.Create();
        var attempts = new BatchAttemptRepository(store, [store.Attempt]);
        var dispatches = new BatchDispatchRepository(store, [], []);
        var detail = await CreateReader(store, attempts, dispatches).GetDetail(store.Instance.Id.ToString());
        Assert.Null(Assert.Single(Assert.Single(detail.Tasks).Attempts).Dispatch);
    }

    [Fact]
    public async Task DetailUsesTheMessageMetadataPersistedForEachDispatch()
    {
        var store = RuntimeDiagnosticsStore.Create();
        store.Dispatch.Metadata["Runtime.Delivery.Command"] = JsonNode.Parse("""
            {"messageMetadata":{"attempt":1,"sagaId":"saved-saga"},
             "propagationMetadata":{"items":{"audit.context":{"requestId":"saved-request"}}}}
            """)!;
        var detail = await CreateReader(store).GetDetail(store.Instance.Id.ToString());
        var dispatch = Assert.Single(Assert.Single(detail.Tasks).Attempts).Dispatch;
        Assert.Equal("Persisted", dispatch.MessageMetadataSource);
        Assert.Contains("saved-saga", dispatch.MessageMetadata);
        Assert.Contains("saved-request", dispatch.PropagationMetadata);
        Assert.DoesNotContain("Runtime.Delivery.Command", dispatch.PropagationMetadata);
    }

    [Fact]
    public async Task DetailLabelsLegacyMetadataAsReconstructed()
    {
        var store = RuntimeDiagnosticsStore.Create();
        store.Dispatch.Metadata.Clear();
        var detail = await CreateReader(store).GetDetail(store.Instance.Id.ToString());
        var dispatch = Assert.Single(Assert.Single(detail.Tasks).Attempts).Dispatch;
        Assert.Equal("Reconstructed", dispatch.MessageMetadataSource);
    }

    [Fact]
    public async Task SnapshotUsesSummaryReaderWithoutChangingRows()
    {
        var store = RuntimeDiagnosticsStore.Create();
        var expected = await CreateReader(store).GetSnapshot();
        var summaries = new SummaryInstanceRepository(store);
        var actual = await CreateReader(store, instances: summaries).GetSnapshot();
        Assert.Equal(expected.Instances, actual.Instances);
        Assert.Equal(1, summaries.Calls);
    }

    private static RuntimeDiagnosticsReader CreateReader(RuntimeDiagnosticsStore store,
        ITaskExecutionAttemptRepository? attempts = null, ITaskDispatchRepository? dispatches = null,
        IOrchestrationInstanceRepository? instances = null)
    {
        return new RuntimeDiagnosticsReader(
            new ArtifactRepositoryStub(store),
            instances ?? new InstanceRepositoryStub(store),
            new StageRepositoryStub(store),
            new TaskRepositoryStub(store),
            attempts ?? new AttemptRepositoryStub(store),
            dispatches ?? new DispatchRepositoryStub(store),
            new CompensationRepositoryStub(store),
            new TransitionRepositoryStub(store));
    }

    private sealed class RuntimeDiagnosticsStore
    {
        private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

        public required RuntimeOrchestrationArtifact Artifact { get; init; }
        public required OrchestrationInstance Instance { get; init; }
        public required StageExecution Stage { get; init; }
        public required TaskExecution Task { get; init; }
        public required TaskExecutionAttempt Attempt { get; init; }
        public required TaskDispatch Dispatch { get; init; }
        public required CompensationExecution Compensation { get; init; }
        public required ExecutionTransition OlderTransition { get; init; }
        public required ExecutionTransition NewerTransition { get; init; }
        public bool ArtifactLookupFails { get; set; }
        public IReadOnlyCollection<RuntimeTrafficPoint> TrafficPoints { get; set; } = [];
        public IReadOnlyCollection<TaskExecution> ExtraTasks { get; set; } = [];

        public static RuntimeDiagnosticsStore Create()
        {
            var instanceId = Id.New();
            var artifactId = Id.New();
            var stageId = Id.New();
            var taskId = Id.New();
            var attemptId = Id.New();
            var dispatchId = Id.New();
            var started = new DateTime(2026, 8, 12, 12, 0, 0, DateTimeKind.Utc);
            var artifact = BuildArtifact(stageId, taskId);

            return new RuntimeDiagnosticsStore
            {
                Artifact = new RuntimeOrchestrationArtifact
                {
                    Id = artifactId,
                    OrchestrationDefinitionKey = artifact.Key,
                    ArtifactType = "orchestration-version-snapshot",
                    SourceOrchestrationVersionId = artifact.OrchestrationVersionId,
                    Version = artifact.Version,
                    ArtifactChecksum = artifact.Checksum,
                    ArtifactPayload = JsonSerializer.SerializeToNode(artifact, SerializerOptions)!,
                    IsActive = true,
                    LoadedToCache = true,
                    DeployedOnUtc = started.AddMinutes(-1),
                    ActivatedOnUtc = started.AddMinutes(-1),
                    Notes = "Runtime diagnostics test artifact."
                },
                Instance = new OrchestrationInstance
                {
                    Id = instanceId,
                    OrchestrationDefinitionKey = "orders",
                    RuntimeOrchestrationArtifactId = artifactId,
                    TriggerIntakeId = Id.New(),
                    CorrelationId = "corr-123",
                    SagaId = "saga-123",
                    ExecutionKey = "orders:corr-123",
                    Status = OrchestrationInstanceStatus.Completed,
                    CurrentStageKey = "reserve-stock",
                    CurrentTaskKey = "reserve",
                    LastExecutedStageKey = "reserve-stock",
                    LastExecutedTaskKey = "reserve",
                    StartedOnUtc = started,
                    LastUpdatedOnUtc = started.AddSeconds(5),
                    CompletedOnUtc = started.AddSeconds(5),
                    SnapshotPayload = JsonNode.Parse("""{"orderId":"A1"}""")
                },
                Stage = new StageExecution
                {
                    Id = stageId,
                    OrchestrationInstanceId = instanceId,
                    StageKey = "reserve-stock",
                    Order = 1,
                    Status = StageExecutionStatus.Completed,
                    StartedOnUtc = started,
                    CompletedOnUtc = started.AddSeconds(4)
                },
                Task = new TaskExecution
                {
                    Id = taskId,
                    OrchestrationInstanceId = instanceId,
                    StageExecutionId = stageId,
                    TaskKey = "reserve",
                    TaskKind = TaskKind.Messaging,
                    ExecutionMode = TaskExecutionMode.Sequential,
                    Status = TaskExecutionStatus.Completed,
                    OnErrorPolicy = OnErrorPolicy.StopAndCompensate,
                    AwaitResponse = true,
                    StartedOnUtc = started.AddSeconds(1),
                    CompletedOnUtc = started.AddSeconds(3),
                    LastAttemptNumber = 1,
                    CorrelationId = "task-corr",
                    OutputVariablesPayload = JsonNode.Parse("""{"reserved":true}""")
                },
                Attempt = new TaskExecutionAttempt
                {
                    Id = attemptId,
                    TaskExecutionId = taskId,
                    AttemptNumber = 1,
                    Status = TaskExecutionStatus.Completed,
                    StartedOnUtc = started.AddSeconds(1),
                    CompletedOnUtc = started.AddSeconds(3),
                    RequestPayload = JsonNode.Parse("""{"sku":"SKU-1"}"""),
                    ResponsePayload = JsonNode.Parse("""{"ok":true}"""),
                    DispatchId = dispatchId
                },
                Dispatch = new TaskDispatch
                {
                    Id = dispatchId,
                    TaskExecutionAttemptId = attemptId,
                    DispatchType = "Messaging",
                    Destination = "orders.reserve",
                    DispatchStatus = "Sent",
                    CommandId = "cmd-1",
                    CorrelationId = "task-corr",
                    SentOnUtc = started.AddSeconds(1),
                    RequestPayload = JsonNode.Parse("""{"sku":"SKU-1"}""")
                },
                Compensation = new CompensationExecution
                {
                    Id = Id.New(),
                    OrchestrationInstanceId = instanceId,
                    SourceTaskExecutionId = taskId,
                    CompensationTaskKey = "release-stock",
                    Status = "Pending",
                    RequestPayload = JsonNode.Parse("""{"sku":"SKU-1"}""")
                },
                OlderTransition = new ExecutionTransition
                {
                    Id = Id.New(),
                    OrchestrationInstanceId = instanceId,
                    StageExecutionId = stageId,
                    TaskExecutionId = taskId,
                    TaskExecutionAttemptId = attemptId,
                    TransitionType = "TaskCompleted",
                    FromStatus = "Running",
                    ToStatus = "Completed",
                    OccurredOnUtc = started.AddSeconds(3),
                    Message = "Task completed",
                    ProducedBy = "Runtime"
                },
                NewerTransition = new ExecutionTransition
                {
                    Id = Id.New(),
                    OrchestrationInstanceId = instanceId,
                    StageExecutionId = stageId,
                    TaskExecutionId = taskId,
                    TaskExecutionAttemptId = attemptId,
                    TransitionType = "TaskDispatchCreated",
                    FromStatus = "Pending",
                    ToStatus = "Sent",
                    OccurredOnUtc = started.AddSeconds(4),
                    Message = "Dispatch sent",
                    ProducedBy = "Runtime"
                }
            };
        }

        public void UseArtifactWithPendingConfiguredItems()
        {
            var artifact = BuildArtifact(Stage.Id, Task.Id, includePendingConfiguredItems: true);
            Artifact.Version = artifact.Version;
            Artifact.ArtifactPayload = JsonSerializer.SerializeToNode(artifact, SerializerOptions)!;
        }

        private static OrchestrationArtifact BuildArtifact(
            Id stageId,
            Id taskId,
            bool includePendingConfiguredItems = false)
        {
            var version = new SemanticVersion(1, 0, 0);
            var schemaBinding = new SchemaBindingArtifact(
                taskId,
                ElementType.Task,
                stageId,
                taskId,
                "orders.reserve",
                version,
                Id.New(),
                false)
            {
                IsValidationEnabled = false
            };

            var configuration = new MessagingTaskConfigurationArtifact("orders.reserve", version, schemaBinding);
            var reserveTasks = new List<TaskArtifact>
            {
                new(
                    taskId,
                    "reserve",
                    "Reserve inventory",
                    1,
                    string.Empty,
                    TaskKind.Messaging,
                    TaskExecutionMode.Sequential,
                    null,
                    DisabledCondition(),
                    DisabledTransformation(),
                    configuration,
                    DefaultRetryPolicy(),
                    DefaultTimeoutPolicy(),
                    OnErrorPolicy.StopAndCompensate,
                    DefaultCompensation(configuration),
                    TaskDispatchType.FireAndWaitCallback,
                    true)
            };

            if (includePendingConfiguredItems)
            {
                reserveTasks.Add(new TaskArtifact(
                    Id.New(),
                    "notify-customer",
                    "Notify customer",
                    2,
                    "Configured but not reached yet.",
                    TaskKind.Messaging,
                    TaskExecutionMode.Sequential,
                    null,
                    DisabledCondition(),
                    DisabledTransformation(),
                    configuration,
                    DefaultRetryPolicy(),
                    DefaultTimeoutPolicy(),
                    OnErrorPolicy.Stop,
                    null,
                    TaskDispatchType.FireAndForget,
                    true));
            }

            var stages = new List<StageArtifact>
            {
                new(
                    stageId,
                    "reserve-stock",
                    "Reserve stock",
                    1,
                    true,
                    DisabledCondition(),
                    reserveTasks,
                    [],
                    [],
                    "Reserve inventory before charging the order.")
            };

            if (includePendingConfiguredItems)
            {
                stages.Add(new StageArtifact(
                    Id.New(),
                    "capture-payment",
                    "Capture payment",
                    2,
                    true,
                    DisabledCondition(),
                    [
                        new TaskArtifact(
                            Id.New(),
                            "charge-card",
                            "Charge card",
                            1,
                            "Configured future stage.",
                            TaskKind.Messaging,
                            TaskExecutionMode.Sequential,
                            null,
                            DisabledCondition(),
                            DisabledTransformation(),
                            configuration,
                            DefaultRetryPolicy(),
                            DefaultTimeoutPolicy(),
                            OnErrorPolicy.Stop,
                            null,
                            TaskDispatchType.FireAndWaitCallback,
                            true)
                    ],
                    [],
                    [],
                    "Capture money once stock is reserved."));
            }

            return new OrchestrationArtifact(
                Id.New(),
                Id.New(),
                "orders",
                "Orders",
                "sales",
                version,
                new Checksum("test"),
                [],
                [],
                stages,
                "Test artifact.");
        }

        private static ExecutionConditionArtifact DisabledCondition()
            => new(EngineType.DSL, new DslConditionConfigurationArtifact(new Expression("true")))
            {
                IsEnabled = false
            };

        private static TransformationArtifact DisabledTransformation()
            => new(EngineType.DSL, new DslTransformationConfigurationArtifact())
            {
                IsEnabled = false
            };

        private static RetryPolicyArtifact DefaultRetryPolicy()
            => new(
                0,
                RetryStrategyType.Fixed,
                new FixedRetryStrategyArtifact(Duration.FromSeconds(1)),
                [],
                true);

        private static TimeoutPolicyArtifact DefaultTimeoutPolicy()
            => new(
                Duration.FromMinutes(5),
                TimeoutBehavior.Fail,
                new FailTimeoutBehaviorPolicyArtifact("TASK_TIMEOUT"));

        private static CompensationArtifact DefaultCompensation(ITaskConfigurationArtifact configuration)
            => new(
                TaskKind.Messaging,
                DisabledTransformation(),
                DisabledCondition(),
                configuration,
                DefaultRetryPolicy(),
                DefaultTimeoutPolicy(),
                TaskDispatchType.FireAndForget);
    }

    private sealed class ArtifactRepositoryStub(RuntimeDiagnosticsStore store) : IRuntimeArtifactRepository
    {
        public Task Upsert(RuntimeOrchestrationArtifact artifact, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task MarkProjectionStarted(Id artifactId, long ingressGeneration, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task MarkReady(Id artifactId, long ingressGeneration, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task MarkProjectionFailed(Id artifactId, long ingressGeneration, string error, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DeactivateActiveArtifacts(string orchestrationDefinitionKey, Id exceptArtifactId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<RuntimeOrchestrationArtifact> GetById(Id artifactId, CancellationToken cancellationToken = default)
            => store.ArtifactLookupFails
                ? Task.FromException<RuntimeOrchestrationArtifact>(new InvalidOperationException("Artifact not available."))
                : Task.FromResult(store.Artifact);
        public Task<RuntimeOrchestrationArtifact> GetByVersion(string orchestrationDefinitionKey, SemanticVersion version, CancellationToken cancellationToken = default) => Task.FromResult(store.Artifact);
        public Task<IReadOnlyCollection<RuntimeOrchestrationArtifact>> GetAll(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<RuntimeOrchestrationArtifact>>([store.Artifact]);
        public Task<IReadOnlyCollection<RuntimeOrchestrationArtifact>> GetReady(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<RuntimeOrchestrationArtifact>>([store.Artifact]);
        public Task<RuntimeOrchestrationArtifact> GetActive(string orchestrationDefinitionKey, CancellationToken cancellationToken = default) => Task.FromResult(store.Artifact);
    }

    private sealed class SummaryInstanceRepository(RuntimeDiagnosticsStore store)
        : InstanceRepositoryStub(store), IRuntimeInstanceRecentSummaryReader
    {
        public int Calls { get; private set; }
        public Task<IReadOnlyCollection<OrchestrationInstance>> GetRecentSummaries(int take, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult<IReadOnlyCollection<OrchestrationInstance>>([store.Instance]);
        }
    }

    private class InstanceRepositoryStub(RuntimeDiagnosticsStore store) : IOrchestrationInstanceRepository
    {
        public Task Create(OrchestrationInstance instance, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task Update(OrchestrationInstance instance, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<OrchestrationInstance> GetById(Id instanceId, CancellationToken cancellationToken = default) => Task.FromResult(store.Instance);
        public Task<OrchestrationInstance> TryGetByStartIdempotencyKey(string startIdempotencyKey, CancellationToken cancellationToken = default) => Task.FromResult<OrchestrationInstance>(null!);
        public Task<OrchestrationInstanceLease> TryAcquireLease(Id instanceId, string leaseId, DateTime nowUtc, DateTime expiresOnUtc, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task ReleaseLease(Id instanceId, string leaseId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyCollection<OrchestrationInstance>> GetRecent(int take = 50, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<OrchestrationInstance>>([store.Instance]);
        public Task<IReadOnlyCollection<OrchestrationInstance>> GetRecoverable(int take = 100, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<OrchestrationInstance>>([store.Instance]);
        public Task<RuntimeInstanceSummary> GetSummary(DateTime recentSinceUtc, CancellationToken cancellationToken = default) => Task.FromResult(new RuntimeInstanceSummary(0, 0, 1, 0, recentSinceUtc));
    }

    private sealed class StageRepositoryStub(RuntimeDiagnosticsStore store) : IStageExecutionRepository
    {
        public Task Create(StageExecution stageExecution, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task Update(StageExecution stageExecution, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<StageExecution> GetById(Id stageExecutionId, CancellationToken cancellationToken = default) => Task.FromResult(store.Stage);
        public Task<StageExecution> GetByInstanceAndKey(Id instanceId, string stageKey, CancellationToken cancellationToken = default) => Task.FromResult(store.Stage);
        public Task<IReadOnlyCollection<StageExecution>> GetByInstanceId(Id instanceId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<StageExecution>>([store.Stage]);
    }

    private sealed class TaskRepositoryStub(RuntimeDiagnosticsStore store) : ITaskExecutionRepository
    {
        public Task Create(TaskExecution taskExecution, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task Update(TaskExecution taskExecution, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<TaskExecution> GetById(Id taskExecutionId, CancellationToken cancellationToken = default) => Task.FromResult(store.Task);
        public Task<TaskExecution> GetByCorrelationId(string correlationId, CancellationToken cancellationToken = default) => Task.FromResult(store.Task);
        public Task<TaskExecution> GetByStageAndKey(Id stageExecutionId, string taskKey, CancellationToken cancellationToken = default) => Task.FromResult(store.Task);
        public Task<IReadOnlyCollection<TaskExecution>> GetByInstanceId(Id instanceId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<TaskExecution>>([store.Task, .. store.ExtraTasks]);
        public Task<IReadOnlyCollection<TaskExecution>> GetWaitingResponseOlderThan(DateTime dueBeforeUtc, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<TaskExecution>>(Array.Empty<TaskExecution>());
    }

    private class AttemptRepositoryStub(RuntimeDiagnosticsStore store) : ITaskExecutionAttemptRepository
    {
        public Task Create(TaskExecutionAttempt attempt, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task Update(TaskExecutionAttempt attempt, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<TaskExecutionAttempt> GetById(Id attemptId, CancellationToken cancellationToken = default) => Task.FromResult(store.Attempt);
        public Task<TaskExecutionAttempt> GetByDispatchId(Id dispatchId, CancellationToken cancellationToken = default) => Task.FromResult(store.Attempt);
        public Task<IReadOnlyCollection<TaskExecutionAttempt>> GetByTaskExecutionId(Id taskExecutionId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<TaskExecutionAttempt>>([store.Attempt]);
        public Task<IReadOnlyCollection<TaskExecutionAttempt>> GetWaitingResponseOlderThan(DateTime dueBeforeUtc, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<TaskExecutionAttempt>>(Array.Empty<TaskExecutionAttempt>());
    }

    private sealed class BatchAttemptRepository(RuntimeDiagnosticsStore store, IReadOnlyCollection<TaskExecutionAttempt> attempts)
        : AttemptRepositoryStub(store), ITaskExecutionAttemptBatchReader
    {
        public int BatchCalls { get; private set; }
        public Task<IReadOnlyCollection<TaskExecutionAttempt>> GetByTaskExecutionIds(IReadOnlyCollection<Id> ids, CancellationToken cancellationToken = default)
        {
            BatchCalls++;
            return Task.FromResult(attempts);
        }
    }

    private sealed class SummaryAttemptRepository(RuntimeDiagnosticsStore store) : AttemptRepositoryStub(store), ITaskExecutionAttemptBatchReader, ITaskExecutionAttemptSummaryBatchReader
    {
        public int SummaryCalls { get; private set; }
        public int BatchCalls { get; private set; }
        public Task<IReadOnlyCollection<TaskExecutionAttempt>> GetSummariesByTaskExecutionIds(IReadOnlyCollection<Id> ids, CancellationToken cancellationToken = default)
        {
            SummaryCalls++;
            var item = store.Attempt;
            return Task.FromResult<IReadOnlyCollection<TaskExecutionAttempt>>([new TaskExecutionAttempt { Id = item.Id, TaskExecutionId = item.TaskExecutionId, AttemptNumber = item.AttemptNumber, DispatchId = item.DispatchId, Status = item.Status }]);
        }
        public Task<IReadOnlyCollection<TaskExecutionAttempt>> GetByTaskExecutionIds(IReadOnlyCollection<Id> ids, CancellationToken cancellationToken = default)
        {
            BatchCalls++;
            return Task.FromResult<IReadOnlyCollection<TaskExecutionAttempt>>([store.Attempt]);
        }
    }

    private sealed class BatchDispatchRepository(RuntimeDiagnosticsStore store, IReadOnlyCollection<TaskDispatch> byId,
        IReadOnlyCollection<TaskDispatch> byAttempt) : DispatchRepositoryStub(store), ITaskDispatchBatchReader
    {
        public int IdCalls { get; private set; }
        public int AttemptCalls { get; private set; }
        public Task<IReadOnlyCollection<TaskDispatch>> GetByIds(IReadOnlyCollection<Id> ids, CancellationToken cancellationToken = default)
        {
            IdCalls++;
            return Task.FromResult(byId);
        }
        public Task<IReadOnlyCollection<TaskDispatch>> GetByAttemptIds(IReadOnlyCollection<Id> ids, CancellationToken cancellationToken = default)
        {
            AttemptCalls++;
            return Task.FromResult(byAttempt);
        }
    }

    private class DispatchRepositoryStub(RuntimeDiagnosticsStore store) : ITaskDispatchRepository
    {
        public Task Create(TaskDispatch dispatch, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task Update(TaskDispatch dispatch, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task MarkSent(Id dispatchId, string status, DateTime sentOnUtc, string? externalReference = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RecordSent(Id dispatchId, DateTime sentOnUtc, string? externalReference = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task MarkFailed(Id dispatchId, string failureReason, string? externalReference = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<TaskDispatch> GetById(Id dispatchId, CancellationToken cancellationToken = default) => Task.FromResult(store.Dispatch);
        public Task<TaskDispatch> TryGetById(Id dispatchId, CancellationToken cancellationToken = default) => Task.FromResult(store.Dispatch);
        public Task<TaskDispatch> GetByCommandId(string commandId, CancellationToken cancellationToken = default) => Task.FromResult(store.Dispatch);
        public Task<TaskDispatch> GetByAttemptId(Id taskExecutionAttemptId, CancellationToken cancellationToken = default) => Task.FromResult(store.Dispatch);
        public Task<IReadOnlyCollection<TaskDispatch>> GetScheduledOlderThan(DateTime dueBeforeUtc, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<TaskDispatch>>(Array.Empty<TaskDispatch>());
        public Task<IReadOnlyCollection<TaskDispatch>> GetQueuePending(int take, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<TaskDispatch>>(Array.Empty<TaskDispatch>());
    }

    private sealed class CompensationRepositoryStub(RuntimeDiagnosticsStore store) : ICompensationExecutionRepository
    {
        public Task Create(CompensationExecution compensationExecution, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task Update(CompensationExecution compensationExecution, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<CompensationExecution> TryGetById(Id compensationExecutionId, CancellationToken cancellationToken = default) => Task.FromResult(store.Compensation);
        public Task<IReadOnlyCollection<CompensationExecution>> GetByInstanceId(Id instanceId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<CompensationExecution>>([store.Compensation]);
        public Task<IReadOnlyCollection<CompensationExecution>> GetPending(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<CompensationExecution>>(Array.Empty<CompensationExecution>());
    }

    private sealed class TransitionRepositoryStub(RuntimeDiagnosticsStore store) : IExecutionTransitionRepository
    {
        public Task Create(ExecutionTransition transition, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyCollection<ExecutionTransition>> GetByInstanceId(Id instanceId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<ExecutionTransition>>([store.NewerTransition, store.OlderTransition]);
        public Task<IReadOnlyCollection<ExecutionTransition>> GetRecent(int take = 250, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<ExecutionTransition>>([store.NewerTransition, store.OlderTransition]);
        public Task<IReadOnlyCollection<RuntimeTrafficPoint>> GetTraffic(DateTime sinceUtc, CancellationToken cancellationToken = default) => Task.FromResult(store.TrafficPoints);
    }
}
