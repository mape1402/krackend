namespace Krackend.Sagas.Orchestrations.Tests.Runtime;

using System.Text.Json;
using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Artifacts;
using Krackend.Sagas.Orchestrations.Abstractions.Execution;
using Krackend.Sagas.Orchestrations.Abstractions.Extensions;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Artifacts;

public sealed class OrchestrationArtifactMigratorTests
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private static readonly SemanticVersion Version = new(1, 0, 0);

    [Fact]
    public void SerializerMigratesLegacyArtifactsToExtensionReadySchema()
    {
        var payload = JsonSerializer.SerializeToNode(CreateArtifact(), SerializerOptions)!;
        payload.AsObject().Remove("artifactSchemaVersion");
        payload.AsObject().Remove("requiredCapabilities");
        payload.AsObject().Remove("requiredBundles");
        var task = payload["stageDefinitions"]![0]!["taskDefinitions"]![0]!.AsObject();
        task.Remove("extensionKey");
        task.Remove("capabilityKey");
        task.Remove("capabilityVersion");
        task.Remove("runtimeRequirements");
        task.Remove("executionPolicy");

        var migrated = new DefaultRuntimeArtifactSerializer().Deserialize(payload.ToJsonString());
        var migratedTask = migrated.StageDefinitions.Single().TaskDefinitions.Single();

        Assert.Equal(OrchestrationArtifactSchemaVersions.Current, migrated.ArtifactSchemaVersion);
        Assert.Equal(ExtensionConstants.BuiltInExtensionKey, migratedTask.ExtensionKey);
        Assert.Equal(BuiltInCapabilityKeys.MessagingTask, migratedTask.CapabilityKey);
        Assert.Equal("1.0.0", migratedTask.CapabilityVersion);
        Assert.Contains(migrated.RequiredCapabilities, capability =>
            capability.ExtensionKey == ExtensionConstants.BuiltInExtensionKey &&
            capability.CapabilityKey == BuiltInCapabilityKeys.MessagingTask &&
            capability.Kind == "Task");
        Assert.Contains(migrated.RequiredCapabilities, capability =>
            capability.ExtensionKey == ExtensionConstants.BuiltInExtensionKey &&
            capability.CapabilityKey == BuiltInCapabilityKeys.EventTrigger &&
            capability.Kind == "Trigger");
    }

    [Fact]
    public void MigratorPreservesExplicitExternalCapabilityMetadata()
    {
        var task = MessagingTask("send.invoice") with
        {
            ExtensionKey = "contoso.billing",
            CapabilityKey = "task.send-invoice",
            CapabilityVersion = "2.1.0",
            RuntimeRequirements = new ExecutionRuntimeRequirementsArtifact
            {
                RequiresNetwork = true,
                RequiredSecrets = ["billing-api-key"]
            }
        };

        var migrated = new DefaultOrchestrationArtifactMigrator().Migrate(CreateArtifact(task));
        var migratedTask = migrated.StageDefinitions.Single().TaskDefinitions.Single();
        var required = migrated.RequiredCapabilities.Single(capability =>
            capability.ExtensionKey == "contoso.billing" &&
            capability.CapabilityKey == "task.send-invoice");

        Assert.Equal("contoso.billing", migratedTask.ExtensionKey);
        Assert.Equal("task.send-invoice", migratedTask.CapabilityKey);
        Assert.Equal("2.1.0", migratedTask.CapabilityVersion);
        Assert.True(migratedTask.RuntimeRequirements.RequiresNetwork);
        Assert.Equal(new SemanticVersion(2, 1, 0), required.Version);
        Assert.Equal("Task", required.Kind);
    }

    private static OrchestrationArtifact CreateArtifact(TaskArtifact? task = null)
        => new(
            Id.New(),
            Id.New(),
            "sales.invoice",
            "Sales Invoice",
            "sales",
            Version,
            new Checksum($"artifact-migrator-{Guid.NewGuid():N}"),
            [EventTrigger()],
            [],
            [Stage(task ?? MessagingTask("send.invoice"))]);

    private static StageArtifact Stage(TaskArtifact task)
        => new(
            Id.New(),
            "stage-one",
            "Stage One",
            1,
            true,
            DisabledCondition(),
            [task],
            [],
            []);

    private static TriggerBindingArtifact EventTrigger()
        => new(
            Id.New(),
            TriggerType.Event,
            new EventTriggerChannelArtifact(
                new SchemaBindingArtifact(
                    Id.New(),
                    ElementType.Orchestration,
                    Id.New(),
                    Id.New(),
                    "sales.invoice.created",
                    Version,
                    Id.New(),
                    false),
                "events.sales.invoice.created",
                Version),
            true);

    private static TaskArtifact MessagingTask(string key)
        => new(
            Id.New(),
            key,
            key,
            1,
            string.Empty,
            TaskKind.Messaging,
            TaskExecutionMode.Sequential,
            null,
            DisabledCondition(),
            DisabledTransformation(),
            new MessagingTaskConfigurationArtifact($"commands.{key}", Version, null!),
            null,
            null,
            OnErrorPolicy.Stop,
            null,
            TaskDispatchType.FireAndWaitCallback,
            true);

    private static ExecutionConditionArtifact DisabledCondition()
        => new(EngineType.DSL, new DslConditionConfigurationArtifact(new Expression(string.Empty)))
        {
            IsEnabled = false
        };

    private static TransformationArtifact DisabledTransformation()
        => new(EngineType.DSL, new DslTransformationConfigurationArtifact())
        {
            IsEnabled = false
        };
}

