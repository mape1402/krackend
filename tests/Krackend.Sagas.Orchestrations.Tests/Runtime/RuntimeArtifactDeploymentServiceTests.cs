namespace Krackend.Sagas.Orchestrations.Tests.Runtime;

using System.Text.Json;
using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Artifacts;
using Krackend.Sagas.Orchestrations.Abstractions.Distribution;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Runtime.Distribution;
using Krackend.Sagas.Orchestrations.SchemaRegistry;
using Krackend.Sagas.Orchestrations.Tests.Runtime.Fakes;
using Krackend.Sagas.Orchestrations.Tests.Runtime.Support;

public sealed class RuntimeArtifactDeploymentServiceTests
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private static readonly SemanticVersion Version = new(1, 0, 0);

    [Fact]
    public async Task DeployAsync_WhenExistingReadyArtifactHasSameChecksum_ReturnsReadyWithoutSchedulingProjection()
    {
        var repository = new TestRuntimeArtifactDeploymentRepository();
        var scheduler = new RecordingRuntimeArtifactProjectionScheduler();
        var existing = repository.Add(CreateRuntimeArtifact(
            "sales.sale.created",
            Version,
            "checksum-a",
            RuntimeOrchestrationArtifactStatus.Ready,
            ingressGeneration: 4));
        var package = CreatePackage(
            existing.OrchestrationDefinitionKey,
            existing.Version,
            existing.ArtifactChecksum.Value,
            existing.SourceOrchestrationVersionId);
        var service = CreateService(repository, scheduler);

        var result = await service.DeployAsync(package, "design-a");

        Assert.True(result.Accepted, result.Message);
        Assert.Equal(RuntimeOrchestrationArtifactStatus.Ready.ToString(), result.Status);
        Assert.Equal(existing.Id.ToString(), result.RuntimeArtifactId);
        Assert.Empty(scheduler.Requests);
        Assert.Equal(0, repository.UpsertCount);
    }

    [Fact]
    public async Task DeployAsync_WhenExistingFailedArtifactHasSameChecksum_RequeuesProjectionWithSameGeneration()
    {
        var repository = new TestRuntimeArtifactDeploymentRepository();
        var scheduler = new RecordingRuntimeArtifactProjectionScheduler();
        var existing = repository.Add(CreateRuntimeArtifact(
            "sales.sale.created",
            Version,
            "checksum-a",
            RuntimeOrchestrationArtifactStatus.Failed,
            ingressGeneration: 6));
        existing.ProjectionError = "Previous projection failed.";
        var package = CreatePackage(
            existing.OrchestrationDefinitionKey,
            existing.Version,
            existing.ArtifactChecksum.Value,
            existing.SourceOrchestrationVersionId);
        var service = CreateService(repository, scheduler);

        var result = await service.DeployAsync(package, "design-a");

        Assert.True(result.Accepted, result.Message);
        Assert.Equal(RuntimeOrchestrationArtifactStatus.Pending.ToString(), result.Status);
        Assert.Single(scheduler.Requests);
        Assert.Equal(existing.Id.ToString(), scheduler.Requests[0].ArtifactId);
        Assert.Equal(existing.IngressGeneration, scheduler.Requests[0].IngressGeneration);
        Assert.Equal(1, repository.UpsertCount);
    }

    [Fact]
    public async Task DeployAsync_WhenExistingVersionHasDifferentChecksum_RejectsWithoutChangingRuntimeState()
    {
        var repository = new TestRuntimeArtifactDeploymentRepository();
        var scheduler = new RecordingRuntimeArtifactProjectionScheduler();
        var existing = repository.Add(CreateRuntimeArtifact(
            "sales.sale.created",
            Version,
            "checksum-a",
            RuntimeOrchestrationArtifactStatus.Ready,
            ingressGeneration: 2));
        var package = CreatePackage(
            existing.OrchestrationDefinitionKey,
            existing.Version,
            "checksum-b",
            existing.SourceOrchestrationVersionId);
        var service = CreateService(repository, scheduler);

        var result = await service.DeployAsync(package, "design-a");

        Assert.False(result.Accepted);
        Assert.Equal("Rejected", result.Status);
        Assert.Contains("different checksum", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(scheduler.Requests);
        Assert.Equal(0, repository.UpsertCount);
        Assert.Equal("checksum-a", repository.Artifacts.Single().ArtifactChecksum.Value);
    }

    [Fact]
    public async Task DeployAsync_WhenPackageChecksumDoesNotMatchPayloadChecksum_RejectsWithoutSchedulingProjection()
    {
        var repository = new TestRuntimeArtifactDeploymentRepository();
        var scheduler = new RecordingRuntimeArtifactProjectionScheduler();
        var versionId = Id.New();
        var package = CreatePackage(
            "sales.sale.created",
            Version,
            "package-checksum",
            versionId,
            payloadChecksum: "payload-checksum");
        var service = CreateService(repository, scheduler);

        var result = await service.DeployAsync(package, "design-a");

        Assert.False(result.Accepted);
        Assert.Equal("Rejected", result.Status);
        Assert.Contains("checksum", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(scheduler.Requests);
        Assert.Empty(repository.Artifacts);
    }

    [Fact]
    public async Task DeployAsync_WhenPackagePayloadHasDifferentOrchestrationKey_RejectsWithoutSchedulingProjection()
    {
        var repository = new TestRuntimeArtifactDeploymentRepository();
        var scheduler = new RecordingRuntimeArtifactProjectionScheduler();
        var versionId = Id.New();
        var package = CreatePackage(
            "sales.sale.created",
            Version,
            "checksum-a",
            versionId,
            payloadKey: "payments.payment.captured");
        var service = CreateService(repository, scheduler);

        var result = await service.DeployAsync(package, "design-a");

        Assert.False(result.Accepted);
        Assert.Equal("Rejected", result.Status);
        Assert.Contains("key", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(scheduler.Requests);
        Assert.Empty(repository.Artifacts);
    }

    [Fact]
    public async Task DeployAsync_WhenPackagePayloadHasDifferentVersion_RejectsWithoutSchedulingProjection()
    {
        var repository = new TestRuntimeArtifactDeploymentRepository();
        var scheduler = new RecordingRuntimeArtifactProjectionScheduler();
        var versionId = Id.New();
        var package = CreatePackage(
            "sales.sale.created",
            Version,
            "checksum-a",
            versionId);
        package.PayloadJson = package.PayloadJson.Replace("\"version\":\"1.0.0\"", "\"version\":\"1.0.1\"", StringComparison.Ordinal);
        var service = CreateService(repository, scheduler, new AlwaysCompatibleRuntimeArtifactCompatibilityValidator());

        var result = await service.DeployAsync(package, "design-a");

        Assert.False(result.Accepted);
        Assert.Equal("Rejected", result.Status);
        Assert.Contains("version", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(scheduler.Requests);
        Assert.Empty(repository.Artifacts);
    }

    [Fact]
    public async Task DeployAsync_WhenPackagePayloadHasDifferentVersionId_RejectsWithoutSchedulingProjection()
    {
        var repository = new TestRuntimeArtifactDeploymentRepository();
        var scheduler = new RecordingRuntimeArtifactProjectionScheduler();
        var versionId = Id.New();
        var package = CreatePackage(
            "sales.sale.created",
            Version,
            "checksum-a",
            versionId);
        package.PayloadJson = package.PayloadJson.Replace(
            versionId.ToString(),
            Id.New().ToString(),
            StringComparison.Ordinal);
        var service = CreateService(repository, scheduler, new AlwaysCompatibleRuntimeArtifactCompatibilityValidator());

        var result = await service.DeployAsync(package, "design-a");

        Assert.False(result.Accepted);
        Assert.Equal("Rejected", result.Status);
        Assert.Contains("version id", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(scheduler.Requests);
        Assert.Empty(repository.Artifacts);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task DeployAsync_WhenRequiredPackageFieldIsMissing_RejectsWithoutSchedulingProjection(string missingValue)
    {
        var repository = new TestRuntimeArtifactDeploymentRepository();
        var scheduler = new RecordingRuntimeArtifactProjectionScheduler();
        var package = CreatePackage(
            "sales.sale.created",
            Version,
            "checksum-a",
            Id.New());
        package.ArtifactId = missingValue;
        var service = CreateService(repository, scheduler);

        var result = await service.DeployAsync(package, "design-a");

        Assert.False(result.Accepted);
        Assert.Equal("Rejected", result.Status);
        Assert.Contains("required", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(scheduler.Requests);
        Assert.Empty(repository.Artifacts);
    }

    [Fact]
    public async Task DeployAsync_WhenPackageVersionIsInvalid_RejectsWithoutSchedulingProjection()
    {
        var repository = new TestRuntimeArtifactDeploymentRepository();
        var scheduler = new RecordingRuntimeArtifactProjectionScheduler();
        var package = CreatePackage(
            "sales.sale.created",
            Version,
            "checksum-a",
            Id.New());
        package.Version = "1.x.0";
        var service = CreateService(repository, scheduler);

        var result = await service.DeployAsync(package, "design-a");

        Assert.False(result.Accepted);
        Assert.Equal("Rejected", result.Status);
        Assert.Contains("semantic version", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(scheduler.Requests);
        Assert.Empty(repository.Artifacts);
    }

    [Theory]
    [InlineData("[]", "object")]
    [InlineData("null", "empty")]
    public async Task DeployAsync_WhenPayloadIsNotAnObject_RejectsWithoutSchedulingProjection(
        string payloadJson,
        string expectedMessage)
    {
        var repository = new TestRuntimeArtifactDeploymentRepository();
        var scheduler = new RecordingRuntimeArtifactProjectionScheduler();
        var package = CreatePackage(
            "sales.sale.created",
            Version,
            "checksum-a",
            Id.New());
        package.PayloadJson = payloadJson;
        var service = CreateService(repository, scheduler);

        var result = await service.DeployAsync(package, "design-a");

        Assert.False(result.Accepted);
        Assert.Equal("Rejected", result.Status);
        Assert.Contains(expectedMessage, result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(scheduler.Requests);
        Assert.Empty(repository.Artifacts);
    }

    [Fact]
    public async Task DeployAsync_WhenPayloadRequiredFieldIsMissing_RejectsWithoutSchedulingProjection()
    {
        var repository = new TestRuntimeArtifactDeploymentRepository();
        var scheduler = new RecordingRuntimeArtifactProjectionScheduler();
        var package = CreatePackage(
            "sales.sale.created",
            Version,
            "checksum-a",
            Id.New());
        var payload = JsonNode.Parse(package.PayloadJson)!.AsObject();
        payload.Remove("checksum");
        payload.Remove("Checksum");
        package.PayloadJson = payload.ToJsonString(SerializerOptions);
        var service = CreateService(repository, scheduler);

        var result = await service.DeployAsync(package, "design-a");

        Assert.False(result.Accepted);
        Assert.Equal("Rejected", result.Status);
        Assert.Contains("checksum", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(scheduler.Requests);
        Assert.Empty(repository.Artifacts);
    }

    [Fact]
    public async Task DeployAsync_WhenPayloadUsesEnvelopeValues_AcceptsPackage()
    {
        var repository = new TestRuntimeArtifactDeploymentRepository();
        var scheduler = new RecordingRuntimeArtifactProjectionScheduler();
        var versionId = Id.New();
        var package = CreatePackage(
            "sales.sale.created",
            Version,
            "checksum-a",
            versionId,
            stages: [Stage("stage-one", 1, MessagingTask("task.dispatch", 1, TaskDispatchType.FireAndWaitCallback))],
            triggers: [EventTrigger("events.sales.sale.created")]);
        var payload = JsonNode.Parse(package.PayloadJson)!.AsObject();
        payload["key"] = JsonNode.Parse("""{"value":"sales.sale.created"}""");
        payload["version"] = JsonNode.Parse("""{"value":"1.0.0"}""");
        payload["orchestrationVersionId"] = JsonNode.Parse($$"""{"value":"{{versionId}}"}""");
        payload["checksum"] = JsonNode.Parse("""{"value":"checksum-a"}""");
        package.PayloadJson = payload.ToJsonString(SerializerOptions);
        var service = CreateService(repository, scheduler, new AlwaysCompatibleRuntimeArtifactCompatibilityValidator());

        var result = await service.DeployAsync(package, "design-a");

        Assert.True(result.Accepted, result.Message);
        Assert.Equal(RuntimeOrchestrationArtifactStatus.Pending.ToString(), result.Status);
        Assert.Single(scheduler.Requests);
        Assert.Single(repository.Artifacts);
    }

    [Fact]
    public async Task DeployAsync_WhenPackagePayloadIsInvalidJson_RejectsWithoutSchedulingProjection()
    {
        var repository = new TestRuntimeArtifactDeploymentRepository();
        var scheduler = new RecordingRuntimeArtifactProjectionScheduler();
        var package = CreatePackage(
            "sales.sale.created",
            Version,
            "checksum-a",
            Id.New());
        package.PayloadJson = "{";
        var service = CreateService(repository, scheduler);

        var result = await service.DeployAsync(package, "design-a");

        Assert.False(result.Accepted);
        Assert.Equal("Rejected", result.Status);
        Assert.Contains("invalid", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(scheduler.Requests);
        Assert.Empty(repository.Artifacts);
    }

    [Fact]
    public async Task DeployAsync_WhenArtifactContainsUnsupportedTaskKind_RejectsWithoutSchedulingProjection()
    {
        var repository = new TestRuntimeArtifactDeploymentRepository();
        var scheduler = new RecordingRuntimeArtifactProjectionScheduler();
        var package = CreatePackage(
            "sales.sale.created",
            Version,
            "checksum-a",
            Id.New(),
            stages: [Stage("stage-one", 1, HttpTask("task.http", 1))]);
        var service = CreateService(repository, scheduler);

        var result = await service.DeployAsync(package, "design-a");

        Assert.False(result.Accepted);
        Assert.Equal("Rejected", result.Status);
        Assert.Contains("TaskKindNotSupported", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(scheduler.Requests);
        Assert.Empty(repository.Artifacts);
    }

    [Fact]
    public async Task DeployAsync_WhenMessagingTaskUsesSynchronousDispatch_RejectsWithoutSchedulingProjection()
    {
        var repository = new TestRuntimeArtifactDeploymentRepository();
        var scheduler = new RecordingRuntimeArtifactProjectionScheduler();
        var package = CreatePackage(
            "sales.sale.created",
            Version,
            "checksum-a",
            Id.New(),
            stages: [Stage("stage-one", 1, MessagingTask("task.sync", 1, TaskDispatchType.FireAndWait))]);
        var service = CreateService(repository, scheduler);

        var result = await service.DeployAsync(package, "design-a");

        Assert.False(result.Accepted);
        Assert.Equal("Rejected", result.Status);
        Assert.Contains("MessagingDispatchTypeNotSupported", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(scheduler.Requests);
        Assert.Empty(repository.Artifacts);
    }

    [Fact]
    public async Task DeployAsync_WhenMessagingTaskUsesUnsupportedRetryStrategy_RejectsWithoutSchedulingProjection()
    {
        var repository = new TestRuntimeArtifactDeploymentRepository();
        var scheduler = new RecordingRuntimeArtifactProjectionScheduler();
        var retryPolicy = new RetryPolicyArtifact(
            1,
            RetryStrategyType.Exponential,
            new FixedRetryStrategyArtifact(Duration.FromSeconds(1)),
            [],
            true);
        var package = CreatePackage(
            "sales.sale.created",
            Version,
            "checksum-a",
            Id.New(),
            stages: [Stage("stage-one", 1, MessagingTask("task.retry", 1, TaskDispatchType.FireAndWaitCallback, retryPolicy))]);
        var service = CreateService(repository, scheduler);

        var result = await service.DeployAsync(package, "design-a");

        Assert.False(result.Accepted);
        Assert.Equal("Rejected", result.Status);
        Assert.Contains("RetryStrategyNotSupported", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(scheduler.Requests);
        Assert.Empty(repository.Artifacts);
    }

    [Fact]
    public async Task DeployAsync_WhenMessagingTaskUsesInvalidTimeout_RejectsWithoutSchedulingProjection()
    {
        var repository = new TestRuntimeArtifactDeploymentRepository();
        var scheduler = new RecordingRuntimeArtifactProjectionScheduler();
        var timeoutPolicy = new TimeoutPolicyArtifact(
            Duration.FromSeconds(0),
            TimeoutBehavior.Fail,
            new FailTimeoutBehaviorPolicyArtifact("TIMEOUT"));
        var package = CreatePackage(
            "sales.sale.created",
            Version,
            "checksum-a",
            Id.New(),
            stages: [Stage("stage-one", 1, MessagingTask("task.timeout", 1, TaskDispatchType.FireAndWaitCallback, timeoutPolicy: timeoutPolicy))]);
        var service = CreateService(repository, scheduler);

        var result = await service.DeployAsync(package, "design-a");

        Assert.False(result.Accepted);
        Assert.Equal("Rejected", result.Status);
        Assert.Contains("TimeoutDurationInvalid", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(scheduler.Requests);
        Assert.Empty(repository.Artifacts);
    }

    [Fact]
    public async Task DeployAsync_WhenMessagingTaskTimeoutBehaviorDoesNotMatchPayload_RejectsWithoutSchedulingProjection()
    {
        var repository = new TestRuntimeArtifactDeploymentRepository();
        var scheduler = new RecordingRuntimeArtifactProjectionScheduler();
        var timeoutPolicy = new TimeoutPolicyArtifact(
            Duration.FromSeconds(30),
            TimeoutBehavior.Fail,
            new WaitTimeoutBehaviorPolicyArtifact(OrchestrationActionOnTimeout.Block, Duration.FromSeconds(10)));
        var package = CreatePackage(
            "sales.sale.created",
            Version,
            "checksum-a",
            Id.New(),
            stages: [Stage("stage-one", 1, MessagingTask("task.timeout", 1, TaskDispatchType.FireAndWaitCallback, timeoutPolicy: timeoutPolicy))]);
        var service = CreateService(repository, scheduler);

        var result = await service.DeployAsync(package, "design-a");

        Assert.False(result.Accepted);
        Assert.Equal("Rejected", result.Status);
        Assert.Contains("TimeoutBehaviorMismatch", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(scheduler.Requests);
        Assert.Empty(repository.Artifacts);
    }

    [Fact]
    public async Task DeployAsync_WhenMessagingTriggerTopicIsMissing_RejectsWithoutSchedulingProjection()
    {
        var repository = new TestRuntimeArtifactDeploymentRepository();
        var scheduler = new RecordingRuntimeArtifactProjectionScheduler();
        var package = CreatePackage(
            "sales.sale.created",
            Version,
            "checksum-a",
            Id.New(),
            stages: [Stage("stage-one", 1, MessagingTask("task.dispatch", 1, TaskDispatchType.FireAndWaitCallback))],
            triggers: [EventTrigger(string.Empty)]);
        var service = CreateService(repository, scheduler);

        var result = await service.DeployAsync(package, "design-a");

        Assert.False(result.Accepted);
        Assert.Equal("Rejected", result.Status);
        Assert.Contains("TriggerTopicMissing", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(scheduler.Requests);
        Assert.Empty(repository.Artifacts);
    }

    [Fact]
    public async Task DeployAsync_WhenMessagingRequestValidationHasNoDsl_RejectsWithoutSchedulingProjection()
    {
        var repository = new TestRuntimeArtifactDeploymentRepository();
        var scheduler = new RecordingRuntimeArtifactProjectionScheduler();
        var configuration = new MessagingTaskConfigurationArtifact(
            "inventories.reserve",
            Version,
            SchemaBinding(validationEnabled: true))
        {
            RequestSchemaBinding = SchemaBinding(validationEnabled: true),
            RequestValidation = new ValidationArtifact(
                EngineType.DSL,
                new DslValidationConfigurationArtifact())
            {
                IsEnabled = true
            }
        };
        var package = CreatePackage(
            "sales.sale.created",
            Version,
            "checksum-a",
            Id.New(),
            stages: [Stage("stage-one", 1, MessagingTask("task.dispatch", 1, TaskDispatchType.FireAndWaitCallback, configuration: configuration))],
            triggers: [EventTrigger("events.sales.sale.created")]);
        var service = CreateService(repository, scheduler);

        var result = await service.DeployAsync(package, "design-a");

        Assert.False(result.Accepted);
        Assert.Equal("Rejected", result.Status);
        Assert.Contains("ValidationDslMissing", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(scheduler.Requests);
        Assert.Empty(repository.Artifacts);
    }

    [Fact]
    public async Task DeployAsync_WhenEnabledStageConditionHasNoExpression_RejectsWithoutSchedulingProjection()
    {
        var repository = new TestRuntimeArtifactDeploymentRepository();
        var scheduler = new RecordingRuntimeArtifactProjectionScheduler();
        var package = CreatePackage(
            "sales.sale.created",
            Version,
            "checksum-a",
            Id.New(),
            stages: [StageWithCondition("stage-one", 1, EnabledCondition(string.Empty), MessagingTask("task.dispatch", 1, TaskDispatchType.FireAndWaitCallback))],
            triggers: [EventTrigger("events.sales.sale.created")]);
        var service = CreateService(repository, scheduler);

        var result = await service.DeployAsync(package, "design-a");

        Assert.False(result.Accepted);
        Assert.Equal("Rejected", result.Status);
        Assert.Contains("ConditionExpressionMissing", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(scheduler.Requests);
        Assert.Empty(repository.Artifacts);
    }

    [Fact]
    public async Task DeployAsync_WhenEnabledTaskTransformationHasNoDsl_RejectsWithoutSchedulingProjection()
    {
        var repository = new TestRuntimeArtifactDeploymentRepository();
        var scheduler = new RecordingRuntimeArtifactProjectionScheduler();
        var package = CreatePackage(
            "sales.sale.created",
            Version,
            "checksum-a",
            Id.New(),
            stages: [Stage("stage-one", 1, MessagingTask("task.dispatch", 1, TaskDispatchType.FireAndWaitCallback, transformation: EnabledTransformation(string.Empty)))],
            triggers: [EventTrigger("events.sales.sale.created")]);
        var service = CreateService(repository, scheduler);

        var result = await service.DeployAsync(package, "design-a");

        Assert.False(result.Accepted);
        Assert.Equal("Rejected", result.Status);
        Assert.Contains("TransformationDslMissing", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(scheduler.Requests);
        Assert.Empty(repository.Artifacts);
    }

    [Fact]
    public async Task DeployAsync_WhenMessagingTaskHasNoOptionalPolicies_AcceptsAndSchedulesProjection()
    {
        var repository = new TestRuntimeArtifactDeploymentRepository();
        var scheduler = new RecordingRuntimeArtifactProjectionScheduler();
        var package = CreatePackage(
            "sales.sale.created",
            Version,
            "checksum-a",
            Id.New(),
            stages: [Stage("stage-one", 1, MessagingTask("task.dispatch", 1, TaskDispatchType.FireAndWaitCallback))],
            triggers: [EventTrigger("events.sales.sale.created")]);
        var service = CreateService(repository, scheduler);

        var result = await service.DeployAsync(package, "design-a");

        Assert.True(result.Accepted, result.Message);
        Assert.Equal(RuntimeOrchestrationArtifactStatus.Pending.ToString(), result.Status);
        Assert.Single(repository.Artifacts);
        Assert.Single(scheduler.Requests);
    }

    [Fact]
    public async Task DeployAsync_WhenMessagingCompensationWaitsForCallback_RejectsWithoutSchedulingProjection()
    {
        var repository = new TestRuntimeArtifactDeploymentRepository();
        var scheduler = new RecordingRuntimeArtifactProjectionScheduler();
        var package = CreatePackage(
            "sales.sale.created",
            Version,
            "checksum-a",
            Id.New(),
            stages: [Stage("stage-one", 1, MessagingTask("task.compensated", 1, TaskDispatchType.FireAndWaitCallback, compensation: Compensation("task.compensated.undo", TaskDispatchType.FireAndWaitCallback)))]);
        var service = CreateService(repository, scheduler);

        var result = await service.DeployAsync(package, "design-a");

        Assert.False(result.Accepted);
        Assert.Equal("Rejected", result.Status);
        Assert.Contains("CompensationDispatchTypeNotSupported", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(scheduler.Requests);
        Assert.Empty(repository.Artifacts);
    }

    private static RuntimeArtifactDeploymentService CreateService(
        TestRuntimeArtifactDeploymentRepository repository,
        RecordingRuntimeArtifactProjectionScheduler scheduler,
        IRuntimeArtifactCompatibilityValidator? compatibilityValidator = null)
        => new(
            repository,
            new NoopRuntimeStorageUnitOfWork(),
            scheduler,
            compatibilityValidator ?? new MessagingRuntimeArtifactCompatibilityValidator());

    private static RuntimeOrchestrationArtifact CreateRuntimeArtifact(
        string key,
        SemanticVersion version,
        string checksum,
        RuntimeOrchestrationArtifactStatus status,
        long ingressGeneration)
    {
        var versionId = Id.New();
        return new RuntimeOrchestrationArtifact
        {
            Id = Id.New(),
            OrchestrationDefinitionKey = key,
            ArtifactType = "orchestration-version-snapshot",
            SourceOrchestrationVersionId = versionId,
            Version = version,
            ArtifactChecksum = new Checksum(checksum),
            ArtifactPayload = JsonSerializer.SerializeToNode(CreateArtifact(key, version, checksum, versionId), SerializerOptions)!,
            Status = status,
            IngressGeneration = ingressGeneration,
            IsActive = true,
            LoadedToCache = false,
            DeployedOnUtc = DateTime.UtcNow
        };
    }

    private static RuntimeArtifactDeliveryPackage CreatePackage(
        string key,
        SemanticVersion version,
        string checksum,
        Id versionId,
        string payloadChecksum = "",
        string payloadKey = "",
        IReadOnlyList<StageArtifact>? stages = null,
        IReadOnlyList<TriggerBindingArtifact>? triggers = null)
        => new()
        {
            ReleaseTargetId = Id.New().ToString(),
            ArtifactId = Id.New().ToString(),
            ArtifactType = "orchestration-version-snapshot",
            SchemaVersion = "1.0",
            OrchestrationDefinitionId = Id.New().ToString(),
            OrchestrationVersionId = versionId.ToString(),
            OrchestrationDefinitionKey = key,
            Version = version.ToString(),
            Checksum = checksum,
            PayloadJson = JsonSerializer.Serialize(
                CreateArtifact(
                    string.IsNullOrWhiteSpace(payloadKey) ? key : payloadKey,
                    version,
                    string.IsNullOrWhiteSpace(payloadChecksum) ? checksum : payloadChecksum,
                    versionId,
                    stages ?? [],
                    triggers ?? []),
                SerializerOptions),
            CorrelationId = Id.New().ToString(),
            PromotedBy = "tests",
            PromotedOnUtc = DateTime.UtcNow
        };

    private static OrchestrationArtifact CreateArtifact(
        string key,
        SemanticVersion version,
        string checksum,
        Id versionId,
        IReadOnlyList<StageArtifact>? stages = null,
        IReadOnlyList<TriggerBindingArtifact>? triggers = null)
        => new(
            Id.New(),
            versionId,
            key,
            key,
            "sales",
            version,
            new Checksum(checksum),
            triggers ?? [],
            [],
            stages ?? []);

    private static StageArtifact Stage(string key, int order, params TaskArtifact[] tasks)
        => new(Id.New(), key, key, order, DisabledCondition(), tasks, [], []);

    private static StageArtifact StageWithCondition(
        string key,
        int order,
        ExecutionConditionArtifact condition,
        params TaskArtifact[] tasks)
        => new(Id.New(), key, key, order, condition, tasks, [], []);

    private static TaskArtifact MessagingTask(
        string key,
        int order,
        TaskDispatchType dispatchType,
        RetryPolicyArtifact? retryPolicy = null,
        TimeoutPolicyArtifact? timeoutPolicy = null,
        CompensationArtifact? compensation = null,
        MessagingTaskConfigurationArtifact? configuration = null,
        TransformationArtifact? transformation = null)
        => new(
            Id.New(),
            key,
            key,
            order,
            string.Empty,
            TaskKind.Messaging,
            TaskExecutionMode.Sequential,
            null,
            DisabledCondition(),
            transformation ?? DisabledTransformation(),
            configuration ?? new MessagingTaskConfigurationArtifact(key, Version, null!),
            retryPolicy,
            timeoutPolicy,
            OnErrorPolicy.Stop,
            compensation,
            dispatchType,
            true);

    private static CompensationArtifact Compensation(string topic, TaskDispatchType dispatchType)
        => new(
            TaskKind.Messaging,
            DisabledTransformation(),
            DisabledCondition(),
            new MessagingTaskConfigurationArtifact(topic, Version, null!),
            null,
            null,
            dispatchType);

    private static TriggerBindingArtifact EventTrigger(string topic)
        => new(
            Id.New(),
            TriggerType.Event,
            new EventTriggerChannelArtifact(SchemaBinding(validationEnabled: false), topic, Version),
            true);

    private static SchemaBindingArtifact SchemaBinding(bool validationEnabled)
        => new(
            Id.New(),
            ElementType.Task,
            Id.New(),
            Id.New(),
            "sales.sale.created",
            Version,
            Id.New(),
            false)
        {
            IsValidationEnabled = validationEnabled,
            ContractKind = SchemaContractKind.CommandRequest
        };

    private static TaskArtifact HttpTask(string key, int order)
        => new(
            Id.New(),
            key,
            key,
            order,
            string.Empty,
            TaskKind.Http,
            TaskExecutionMode.Sequential,
            null,
            DisabledCondition(),
            DisabledTransformation(),
            new HttpTaskConfigurationArtifact(
                null!,
                "Services:Demo",
                "/demo",
                "POST",
                JsonNode.Parse("{}"),
                JsonNode.Parse("{}"),
                [200],
                true),
            null,
            null,
            OnErrorPolicy.Stop,
            null,
            TaskDispatchType.FireAndWait,
            true);

    private static ExecutionConditionArtifact DisabledCondition()
        => new(EngineType.DSL, new DslConditionConfigurationArtifact(new Expression(string.Empty)))
        {
            IsEnabled = false
        };

    private static ExecutionConditionArtifact EnabledCondition(string expression)
        => new(EngineType.DSL, new DslConditionConfigurationArtifact(new Expression(expression)))
        {
            IsEnabled = true
        };

    private static TransformationArtifact DisabledTransformation()
        => new(EngineType.DSL, new DslTransformationConfigurationArtifact())
        {
            IsEnabled = false
        };

    private static TransformationArtifact EnabledTransformation(string dsl)
        => new(EngineType.DSL, new DslTransformationConfigurationArtifact
        {
            Dsl = dsl
        })
        {
            IsEnabled = true
        };

    private sealed class AlwaysCompatibleRuntimeArtifactCompatibilityValidator : IRuntimeArtifactCompatibilityValidator
    {
        public Task<RuntimeArtifactCompatibilityValidationResult> ValidateAsync(
            JsonNode artifactPayload,
            CancellationToken cancellationToken = default)
            => Task.FromResult(RuntimeArtifactCompatibilityValidationResult.Success());
    }
}
