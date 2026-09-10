namespace Krackend.Sagas.Orchestrations.Tests.Runtime;

using Krackend.Sagas.Orchestrations.Abstractions.Artifacts;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Dispatching;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Payloads;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Transformations;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Validation;
using NSubstitute;
using System.Text.Json.Nodes;

public sealed class TaskDispatchRequestPayloadPreparerTests
{
    [Fact]
    public async Task PrepareAsyncUsesTriggerPayloadWhenDecisionPayloadIsEmpty()
    {
        var preparer = CreatePreparer();

        var result = await preparer.PrepareAsync(new TaskDispatchRequestPayloadPreparationRequest
        {
            Instance = CreateInstance(),
            StageKey = "inventory-reservation",
            Task = CreateTask(),
            MessagingConfiguration = CreateMessagingConfiguration()
        });

        Assert.Equal("sale-1", result.Payload["saleId"]?.GetValue<string>());
    }

    [Fact]
    public async Task PrepareAsyncUsesTransformationPayloadWhenTransformIsEnabled()
    {
        var transformationExecutor = Substitute.For<IOrchestrationTransformationExecutor>();
        transformationExecutor.TransformAsync(
                Arg.Any<OrchestrationTransformationRequest>(),
                Arg.Any<CancellationToken>())
            .Returns(OrchestrationTransformationResult.Success(JsonNode.Parse("""{"inventoryReservationId":"reservation-1"}""")));

        var preparer = CreatePreparer(transformationExecutor: transformationExecutor);

        var result = await preparer.PrepareAsync(new TaskDispatchRequestPayloadPreparationRequest
        {
            Instance = CreateInstance(),
            StageKey = "inventory-reservation",
            Task = CreateTask(transformEnabled: true),
            MessagingConfiguration = CreateMessagingConfiguration()
        });

        Assert.Equal("reservation-1", result.Payload["inventoryReservationId"]?.GetValue<string>());
    }

    [Fact]
    public async Task PrepareAsyncThrowsPreparationExceptionWhenRequestValidationFails()
    {
        var validationExecutor = Substitute.For<IOrchestrationValidationExecutor>();
        validationExecutor.ValidateAsync(
                Arg.Any<OrchestrationValidationRequest>(),
                Arg.Any<CancellationToken>())
            .Returns(OrchestrationValidationResult.Failure(
                "InvalidReserveInventoryRequest",
                "Reserve inventory request is invalid.",
                new Dictionary<string, JsonNode>
                {
                    ["field"] = JsonValue.Create("saleId")
                }));

        var preparer = CreatePreparer(validationExecutor: validationExecutor);

        var exception = await Assert.ThrowsAsync<TaskDispatchPreparationException>(() =>
            preparer.PrepareAsync(new TaskDispatchRequestPayloadPreparationRequest
            {
                Instance = CreateInstance(),
                StageKey = "inventory-reservation",
                Task = CreateTask(),
                MessagingConfiguration = CreateMessagingConfiguration(validationEnabled: true)
            }));

        Assert.Equal("InvalidReserveInventoryRequest", exception.ErrorCode);
        Assert.Equal("Reserve inventory request is invalid.", exception.Message);
        Assert.True(exception.Diagnostics.ContainsKey("field"));
    }

    private static DefaultTaskDispatchRequestPayloadPreparer CreatePreparer(
        IOrchestrationTransformationExecutor? transformationExecutor = null,
        IOrchestrationValidationExecutor? validationExecutor = null)
        => new(
            new DefaultOrchestrationPayloadContextFactory(),
            transformationExecutor ?? Substitute.For<IOrchestrationTransformationExecutor>(),
            validationExecutor ?? Substitute.For<IOrchestrationValidationExecutor>());

    private static OrchestrationInstance CreateInstance()
        => new()
        {
            Id = Id.New(),
            OrchestrationDefinitionKey = "sales.sale.created",
            RuntimeOrchestrationArtifactId = Id.New(),
            CorrelationId = "sale-1",
            ExecutionKey = "sales.sale.created:sale-1",
            SnapshotPayload = JsonNode.Parse(
                """
                {
                  "trigger": {
                    "payload": {
                      "saleId": "sale-1"
                    }
                  },
                  "stages": {},
                  "variables": {}
                }
                """)
        };

    private static TaskArtifact CreateTask(bool transformEnabled = false)
        => new(
            Id.New(),
            "inventories.reserve",
            "Reserve inventory",
            1,
            string.Empty,
            TaskKind.Messaging,
            TaskExecutionMode.Sequential,
            null,
            null,
            new TransformationArtifact(EngineType.DSL, new DslTransformationConfigurationArtifact())
            {
                IsEnabled = transformEnabled
            },
            CreateMessagingConfiguration(),
            null,
            null,
            OnErrorPolicy.Stop,
            null,
            TaskDispatchType.FireAndWait,
            true);

    private static MessagingTaskConfigurationArtifact CreateMessagingConfiguration(bool validationEnabled = false)
        => new("inventories.reserve", new SemanticVersion(1, 0, 0), null)
        {
            RequestSchemaBinding = new SchemaBindingArtifact(
                Id.New(),
                ElementType.Task,
                Id.New(),
                Id.New(),
                "inventories.reserve.request",
                new SemanticVersion(1, 0, 0),
                Id.New(),
                true)
            {
                IsValidationEnabled = validationEnabled
            }
        };
}
