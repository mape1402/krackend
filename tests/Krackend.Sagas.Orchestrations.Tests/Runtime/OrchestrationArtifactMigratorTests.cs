namespace Krackend.Sagas.Orchestrations.Tests.Runtime;

using System.Reflection;
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

    [Fact]
    public void MigratorNormalizesSparseArtifactsCapabilitiesAndCompensation()
    {
        var migrator = new DefaultOrchestrationArtifactMigrator();
        var nullCollections = CreateArtifact() with
        {
            ExecutionPolicy = null!,
            RequiredCapabilities = null!,
            StageDefinitions = null!,
            TriggerBindings = null!
        };

        var migratedNullCollections = migrator.Migrate(nullCollections);

        Assert.Empty(migratedNullCollections.StageDefinitions);
        Assert.Empty(migratedNullCollections.RequiredCapabilities);
        Assert.Equal(ExecutionPolicyArtifact.Empty, migratedNullCollections.ExecutionPolicy);

        var compensation = new CompensationArtifact(
            TaskKind.Http,
            DisabledTransformation(),
            DisabledCondition(),
            HttpConfiguration(),
            null!,
            null!,
            TaskDispatchType.FireAndForget);
        var customTask = MessagingTask("send.invoice") with
        {
            ExtensionKey = " custom.ext ",
            CapabilityKey = " task.custom ",
            CapabilityVersion = "bad.version",
            Compensation = compensation,
            ExecutionPolicy = null!,
            RuntimeRequirements = null!
        };
        var httpTask = Task("http.customer", TaskKind.Http, HttpConfiguration());
        var pluginTask = Task("plugin.credit", TaskKind.Plugin, new PluginTaskConfigurationArtifact(Id.New()));
        var humanTask = Task("approval.manual", TaskKind.HumanApproval, new HumanApprovalTaskConfigurationArtifact());
        var nullTaskStage = Stage(MessagingTask("ignored")) with { TaskDefinitions = null! };
        var artifact = CreateArtifact(customTask) with
        {
            TriggerBindings = [EventTrigger() with { IsEnabled = false }],
            RequiredCapabilities =
            [
                new RequiredCapabilityArtifact(" ", "cap", Version, "Invalid"),
                new RequiredCapabilityArtifact("ext", " ", Version, "Invalid"),
                new RequiredCapabilityArtifact("kept.ext", "kept.cap", new SemanticVersion(9, 0, 0), "Existing")
            ],
            StageDefinitions =
            [
                Stage(customTask) with { ExecutionPolicy = null!, TaskDefinitions = [customTask, httpTask, pluginTask, humanTask] },
                nullTaskStage
            ]
        };

        var migrated = migrator.Migrate(artifact);
        var migratedCustomTask = migrated.StageDefinitions[0].TaskDefinitions[0];

        Assert.Equal("custom.ext", migratedCustomTask.ExtensionKey);
        Assert.Equal("task.custom", migratedCustomTask.CapabilityKey);
        Assert.Equal("bad.version", migratedCustomTask.CapabilityVersion);
        Assert.Equal(ExecutionPolicyArtifact.Empty, migratedCustomTask.ExecutionPolicy);
        Assert.Equal(ExecutionRuntimeRequirementsArtifact.Empty, migratedCustomTask.RuntimeRequirements);
        Assert.Contains(migrated.RequiredCapabilities, capability =>
            capability.ExtensionKey == "kept.ext" &&
            capability.CapabilityKey == "kept.cap" &&
            capability.Version.Equals(new SemanticVersion(9, 0, 0)));
        Assert.Contains(migrated.RequiredCapabilities, capability =>
            capability.ExtensionKey == "custom.ext" &&
            capability.CapabilityKey == "task.custom" &&
            capability.Version.Equals(Version));
        Assert.Contains(migrated.RequiredCapabilities, capability =>
            capability.Kind == "Compensation" &&
            capability.CapabilityKey == BuiltInCapabilityKeys.HttpTask);
        Assert.Contains(migrated.RequiredCapabilities, capability => capability.CapabilityKey == BuiltInCapabilityKeys.PluginTask);
        Assert.Contains(migrated.RequiredCapabilities, capability => capability.CapabilityKey == BuiltInCapabilityKeys.HumanApprovalTask);
        Assert.DoesNotContain(migrated.RequiredCapabilities, capability => capability.CapabilityKey == BuiltInCapabilityKeys.EventTrigger);
    }

    [Fact]
    public void PrivateHelpersCoverParsingFallbackAndBlankCapabilityBranches()
    {
        Assert.Throws<ArgumentNullException>(() => new DefaultOrchestrationArtifactMigrator().Migrate(null!));
        Assert.Equal(BuiltInCapabilityKeys.HttpTask, InvokePrivate<string>(
            "ResolveBuiltInTaskCapability",
            [typeof(TaskKind)],
            [TaskKind.Http]));
        Assert.Equal(BuiltInCapabilityKeys.PluginTask, InvokePrivate<string>(
            "ResolveBuiltInTaskCapability",
            [typeof(TaskKind)],
            [TaskKind.Plugin]));
        Assert.Equal(BuiltInCapabilityKeys.HumanApprovalTask, InvokePrivate<string>(
            "ResolveBuiltInTaskCapability",
            [typeof(TaskKind)],
            [TaskKind.HumanApproval]));
        Assert.Equal("task.999", InvokePrivate<string>(
            "ResolveBuiltInTaskCapability",
            [typeof(TaskKind)],
            [(TaskKind)999]));
        Assert.Null(InvokePrivate<SemanticVersion?>("TryParseVersion", [typeof(string)], [null]));
        Assert.Null(InvokePrivate<SemanticVersion?>("TryParseVersion", [typeof(string)], [" "]));
        Assert.Null(InvokePrivate<SemanticVersion?>("TryParseVersion", [typeof(string)], ["1.2"]));
        Assert.Null(InvokePrivate<SemanticVersion?>("TryParseVersion", [typeof(string)], ["x.2.3"]));
        Assert.Null(InvokePrivate<SemanticVersion?>("TryParseVersion", [typeof(string)], ["1.x.3"]));
        Assert.Null(InvokePrivate<SemanticVersion?>("TryParseVersion", [typeof(string)], ["1.2.x"]));
        Assert.Equal(new SemanticVersion(1, 2, 3), InvokePrivate<SemanticVersion?>("TryParseVersion", [typeof(string)], ["1.2.3"]));

        var capabilities = new Dictionary<string, RequiredCapabilityArtifact>(StringComparer.OrdinalIgnoreCase);
        InvokePrivate<object?>(
            "AddCapability",
            [typeof(IDictionary<string, RequiredCapabilityArtifact>), typeof(string), typeof(string), typeof(SemanticVersion), typeof(string)],
            [capabilities, " ", "cap", Version, "Task"]);
        InvokePrivate<object?>(
            "AddCapability",
            [typeof(IDictionary<string, RequiredCapabilityArtifact>), typeof(string), typeof(string), typeof(SemanticVersion), typeof(string)],
            [capabilities, "ext", " ", Version, "Task"]);
        InvokePrivate<object?>(
            "AddCapability",
            [typeof(IDictionary<string, RequiredCapabilityArtifact>), typeof(string), typeof(string), typeof(SemanticVersion), typeof(string)],
            [capabilities, " ext ", " cap ", Version, "Task"]);

        var capability = Assert.Single(capabilities.Values);
        Assert.Equal("ext", capability.ExtensionKey);
        Assert.Equal("cap", capability.CapabilityKey);

        var rawTask = MessagingTask("raw") with
        {
            ExtensionKey = " ",
            CapabilityKey = " ",
            CapabilityVersion = null!
        };
        var rawCapabilities = InvokePrivate<IReadOnlyList<RequiredCapabilityArtifact>>(
            "MergeRequiredCapabilities",
            [typeof(OrchestrationArtifact), typeof(IReadOnlyCollection<StageArtifact>), typeof(IReadOnlyCollection<TriggerBindingArtifact>)],
            [
                CreateArtifact(rawTask),
                new[]
                {
                    Stage(rawTask) with { TaskDefinitions = [rawTask] },
                    Stage(MessagingTask("empty")) with { TaskDefinitions = null! }
                },
                Array.Empty<TriggerBindingArtifact>()
            ]);

        Assert.Contains(rawCapabilities, candidate =>
            candidate.ExtensionKey == ExtensionConstants.BuiltInExtensionKey &&
            candidate.CapabilityKey == BuiltInCapabilityKeys.MessagingTask);
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

    private static TaskArtifact Task(string key, TaskKind kind, ITaskConfigurationArtifact configuration)
        => MessagingTask(key) with
        {
            Kind = kind,
            Configuration = configuration,
            DispatchType = TaskDispatchType.FireAndForget
        };

    private static HttpTaskConfigurationArtifact HttpConfiguration()
        => new(
            null!,
            "api",
            "/customers",
            "POST",
            new JsonObject(),
            new JsonObject(),
            [200],
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

    private static TResult InvokePrivate<TResult>(
        string methodName,
        Type[] parameterTypes,
        object?[] arguments)
    {
        var method = typeof(DefaultOrchestrationArtifactMigrator)
            .GetMethod(
                methodName,
                BindingFlags.NonPublic | BindingFlags.Static,
                binder: null,
                types: parameterTypes,
                modifiers: null)!;
        return (TResult)method.Invoke(null, arguments)!;
    }
}
