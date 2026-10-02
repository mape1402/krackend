namespace Krackend.Sagas.Orchestrations.Tests.Runtime;

using Krackend.Sagas.Orchestrations.Abstractions.Artifacts;
using Krackend.Sagas.Orchestrations.Abstractions.Execution;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Execution;
using Krackend.Sagas.Orchestrations.Tests.Runtime.Support;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json.Nodes;

public sealed class ExecutionPolicyDispatchIntegrationTests
{
    private static readonly SemanticVersion Version = new(1, 0, 0);

    [Fact]
    public async Task EngineDispatchesLegacyArtifactThroughBuiltInLocalProviderAndPersistsPolicySnapshot()
    {
        using var harness = await MessagingEngineHarness.CreateAsync(CreateArtifact(
            Stage("stage-one", 1, MessagingTask("task.one", 1))));

        await harness.StartAsync(Payload("trigger"), "correlation-execution-policy");

        var command = harness.Dispatcher.Commands.Single();
        var attempt = await harness.GetAttemptAsync(command);
        var dispatch = await harness
            .GetRequiredService<ITaskDispatchRepository>()
            .GetById(IdFrom(command.DispatchId));

        Assert.Equal(ExecutionConstants.BuiltInLocalProvider, attempt.Metadata["ResolvedExecutionPolicy"]!["providerKey"]!.GetValue<string>());
        Assert.Equal(ExecutionConstants.InProcessTrustedMode, attempt.Metadata["ResolvedExecutionPolicy"]!["executionMode"]!.GetValue<string>());
        Assert.Equal(ExecutionConstants.BuiltInLocalProvider, dispatch.Metadata["ResolvedExecutionPolicy"]!["providerKey"]!.GetValue<string>());
    }

    [Fact]
    public async Task EngineHonorsTaskExecutionPolicyWhenItDoesNotViolateRuntimeDefaults()
    {
        var task = MessagingTask("task.policy", 1) with
        {
            ExecutionPolicy = new ExecutionPolicyArtifact
            {
                DefaultProviderKey = ExecutionConstants.BuiltInLocalProvider,
                TimeoutSeconds = 45,
                MemoryMb = 128
            },
            RuntimeRequirements = new ExecutionRuntimeRequirementsArtifact
            {
                MinMemoryMb = 64
            }
        };
        using var harness = await MessagingEngineHarness.CreateAsync(CreateArtifact(
            Stage("stage-one", 1, task)));

        await harness.StartAsync(Payload("trigger"), "correlation-task-policy");

        var command = harness.Dispatcher.Commands.Single();
        var attempt = await harness.GetAttemptAsync(command);
        var snapshot = attempt.Metadata["ResolvedExecutionPolicy"]!;

        Assert.Equal(ExecutionConstants.BuiltInLocalProvider, snapshot["providerKey"]!.GetValue<string>());
        Assert.Equal(45, snapshot["timeoutSeconds"]!.GetValue<int>());
        Assert.Equal(128, snapshot["memoryMb"]!.GetValue<int>());
    }

    [Fact]
    public async Task EngineFailsDispatchWithActionableErrorWhenPolicyCannotBeResolved()
    {
        var task = MessagingTask("task.external", 1) with
        {
            ExtensionKey = "contoso.billing"
        };
        using var harness = await MessagingEngineHarness.CreateAsync(
            CreateArtifact(Stage("stage-one", 1, task)),
            services => services.Configure<RuntimeExecutionOptions>(options =>
            {
                options.EnvironmentPolicy = new ExecutionPolicyArtifact
                {
                    DefaultProviderKey = ExecutionConstants.BuiltInLocalProvider,
                    RequireSandboxForExternalExtensions = true
                };
            }));

        await harness.StartAsync(Payload("trigger"), "correlation-policy-failure");

        var instance = Assert.Single(await harness
            .GetRequiredService<IOrchestrationInstanceRepository>()
            .GetRecent());
        var tasks = await harness
            .GetRequiredService<ITaskExecutionRepository>()
            .GetByInstanceId(instance.Id);
        var failedTask = Assert.Single(tasks);

        Assert.Empty(harness.Dispatcher.Commands);
        Assert.Equal("ExecutionSandboxRequired", failedTask.Metadata["ExecutionErrorCode"]!.GetValue<string>());
        Assert.Contains("must run in an execution sandbox", failedTask.Metadata["ExecutionErrorMessage"]!.GetValue<string>());
    }

    private static OrchestrationArtifact CreateArtifact(params StageArtifact[] stages)
        => new(
            Id.New(),
            Id.New(),
            "test.execution.policy",
            "Test Execution Policy",
            "test",
            Version,
            new Checksum($"execution-policy-{Guid.NewGuid():N}"),
            [],
            [],
            stages);

    private static StageArtifact Stage(string key, int order, params TaskArtifact[] tasks)
        => new(Id.New(), key, key, order, true, ExecutionCondition(), tasks, [], []);

    private static TaskArtifact MessagingTask(string key, int order)
        => new(
            Id.New(),
            key,
            key,
            order,
            string.Empty,
            TaskKind.Messaging,
            TaskExecutionMode.Sequential,
            null,
            ExecutionCondition(),
            Transformation(),
            new MessagingTaskConfigurationArtifact(key, Version, null!),
            null,
            null,
            OnErrorPolicy.Stop,
            null,
            TaskDispatchType.FireAndWaitCallback,
            true);

    private static TransformationArtifact Transformation()
        => new(EngineType.DSL, new DslTransformationConfigurationArtifact());

    private static ExecutionConditionArtifact ExecutionCondition()
        => new(EngineType.DSL, new DslConditionConfigurationArtifact(new Expression("true")));

    private static JsonNode Payload(string value)
        => JsonNode.Parse($$"""{"value":"{{value}}"}""")!;

    private static Id IdFrom(string value)
        => new(Ulid.Parse(value));
}
