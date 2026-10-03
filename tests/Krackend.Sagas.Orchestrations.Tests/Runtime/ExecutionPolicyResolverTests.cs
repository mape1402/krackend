namespace Krackend.Sagas.Orchestrations.Tests.Runtime;

using Krackend.Sagas.Orchestrations.Abstractions.Artifacts;
using Krackend.Sagas.Orchestrations.Abstractions.Execution;
using Krackend.Sagas.Orchestrations.Abstractions.Extensions;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Runtime.Execution;
using Microsoft.Extensions.Options;

public sealed class ExecutionPolicyResolverTests
{
    private static readonly SemanticVersion Version = new(1, 0, 0);

    [Fact]
    public void ResolveUsesBuiltInLocalProviderByDefault()
    {
        var resolver = CreateResolver(new RuntimeExecutionOptions());

        var result = resolver.Resolve(Request(TaskArtifactFor("task.default")));

        Assert.True(result.Succeeded);
        Assert.Equal(ExecutionConstants.BuiltInLocalProvider, result.ResolvedPolicy.ProviderKey);
        Assert.Equal(ExecutionConstants.InProcessTrustedMode, result.ResolvedPolicy.ExecutionMode);
        Assert.Equal(ExecutionPolicyScope.Environment, result.ResolvedPolicy.ProviderSource);
    }

    [Fact]
    public void ResolveLetsTaskPolicyOverrideStageOrchestrationRuntimeAndEnvironmentDefaults()
    {
        var resolver = CreateResolver(new RuntimeExecutionOptions
        {
            EnvironmentPolicy = Policy("kubernetes"),
            RuntimeNodePolicy = Policy("remote-worker"),
            RuntimeNodeCapabilities = new RuntimeNodeCapabilitiesArtifact
            {
                SupportedProviderKeys =
                [
                    "kubernetes",
                    "remote-worker",
                    ExecutionConstants.BuiltInLocalProvider
                ],
                SupportedExecutionModes =
                [
                    ExecutionConstants.SandboxMode,
                    ExecutionConstants.RemoteWorkerMode,
                    ExecutionConstants.InProcessTrustedMode
                ],
                SupportsNetwork = true
            }
        });

        var task = TaskArtifactFor("task.override") with
        {
            ExecutionPolicy = Policy(ExecutionConstants.BuiltInLocalProvider)
        };
        var result = resolver.Resolve(Request(
            task,
            orchestrationPolicy: Policy("kubernetes"),
            stagePolicy: Policy("remote-worker")));

        Assert.True(result.Succeeded);
        Assert.Equal(ExecutionConstants.BuiltInLocalProvider, result.ResolvedPolicy.ProviderKey);
        Assert.Equal(ExecutionPolicyScope.Task, result.ResolvedPolicy.ProviderSource);
    }

    [Fact]
    public void ResolveRejectsProviderThatViolatesHardConstraint()
    {
        var resolver = CreateResolver(new RuntimeExecutionOptions
        {
            EnvironmentPolicy = new ExecutionPolicyArtifact
            {
                DefaultProviderKey = "kubernetes",
                AllowedProviderKeys = ["kubernetes"]
            },
            RuntimeNodeCapabilities = new RuntimeNodeCapabilitiesArtifact
            {
                SupportedProviderKeys = ["kubernetes", ExecutionConstants.BuiltInLocalProvider],
                SupportedExecutionModes = [ExecutionConstants.SandboxMode, ExecutionConstants.InProcessTrustedMode],
                SupportsNetwork = true
            }
        });

        var task = TaskArtifactFor("task.forbidden") with
        {
            ExecutionPolicy = Policy(ExecutionConstants.BuiltInLocalProvider)
        };
        var result = resolver.Resolve(Request(task));

        Assert.False(result.Succeeded);
        Assert.Equal("ExecutionProviderNotAllowed", result.ErrorCode);
    }

    [Fact]
    public void ResolveRejectsExternalExtensionWhenSandboxIsRequiredAndProviderIsLocal()
    {
        var resolver = CreateResolver(new RuntimeExecutionOptions
        {
            EnvironmentPolicy = new ExecutionPolicyArtifact
            {
                DefaultProviderKey = ExecutionConstants.BuiltInLocalProvider,
                RequireSandboxForExternalExtensions = true
            },
            RuntimeNodeCapabilities = RuntimeNodeCapabilitiesArtifact.LocalDefaults
        });

        var task = TaskArtifactFor("task.external") with
        {
            ExtensionKey = "contoso.billing"
        };
        var result = resolver.Resolve(Request(task));

        Assert.False(result.Succeeded);
        Assert.Equal("ExecutionSandboxRequired", result.ErrorCode);
    }

    [Fact]
    public void ResolveAcceptsExternalExtensionWhenSandboxProviderIsSelected()
    {
        var resolver = CreateResolver(new RuntimeExecutionOptions
        {
            EnvironmentPolicy = new ExecutionPolicyArtifact
            {
                DefaultProviderKey = "kubernetes",
                RequireSandboxForExternalExtensions = true
            },
            RuntimeNodeCapabilities = new RuntimeNodeCapabilitiesArtifact
            {
                SupportedProviderKeys = ["kubernetes"],
                SupportedExecutionModes = [ExecutionConstants.SandboxMode],
                SupportsNetwork = true
            }
        });

        var task = TaskArtifactFor("task.external") with
        {
            ExtensionKey = "contoso.billing"
        };
        var result = resolver.Resolve(Request(task));

        Assert.True(result.Succeeded);
        Assert.Equal("kubernetes", result.ResolvedPolicy.ProviderKey);
        Assert.Equal(ExecutionConstants.SandboxMode, result.ResolvedPolicy.ExecutionMode);
    }

    [Fact]
    public void ResolveRejectsMissingSecretsWhenRuntimeNodeDeclaresSecretInventory()
    {
        var resolver = CreateResolver(new RuntimeExecutionOptions
        {
            RuntimeNodeCapabilities = new RuntimeNodeCapabilitiesArtifact
            {
                SupportedProviderKeys = [ExecutionConstants.BuiltInLocalProvider],
                SupportedExecutionModes = [ExecutionConstants.InProcessTrustedMode],
                SupportsNetwork = true,
                AvailableSecrets = ["crm-api-key"]
            }
        });

        var task = TaskArtifactFor("task.secret") with
        {
            RuntimeRequirements = new ExecutionRuntimeRequirementsArtifact
            {
                RequiredSecrets = ["billing-api-key"]
            }
        };
        var result = resolver.Resolve(Request(task));

        Assert.False(result.Succeeded);
        Assert.Equal("ExecutionSecretUnavailable", result.ErrorCode);
    }

    [Fact]
    public void ResolveAllowsRequiredSecretsWhenRuntimeNodeDoesNotDeclareSecretInventory()
    {
        var resolver = CreateResolver(new RuntimeExecutionOptions
        {
            RuntimeNodeCapabilities = RuntimeNodeCapabilitiesArtifact.LocalDefaults
        });
        var task = TaskArtifactFor("task.secret-open-inventory") with
        {
            RuntimeRequirements = new ExecutionRuntimeRequirementsArtifact
            {
                RequiredSecrets = ["billing-api-key"]
            }
        };

        var result = resolver.Resolve(Request(task));

        Assert.True(result.Succeeded);
        Assert.Equal(ExecutionConstants.BuiltInLocalProvider, result.ResolvedPolicy.ProviderKey);
    }

    [Fact]
    public void ResolveRejectsForbiddenProvider()
    {
        var resolver = CreateResolver(new RuntimeExecutionOptions
        {
            EnvironmentPolicy = new ExecutionPolicyArtifact
            {
                DefaultProviderKey = ExecutionConstants.BuiltInLocalProvider,
                ForbiddenProviderKeys = [ExecutionConstants.BuiltInLocalProvider]
            },
            RuntimeNodeCapabilities = RuntimeNodeCapabilitiesArtifact.LocalDefaults
        });

        var result = resolver.Resolve(Request(TaskArtifactFor("task.forbidden-provider")));

        Assert.False(result.Succeeded);
        Assert.Equal("ExecutionProviderForbidden", result.ErrorCode);
    }

    [Fact]
    public void ResolveRejectsProviderThatIsAllowedButNotConfigured()
    {
        var resolver = CreateResolver(new RuntimeExecutionOptions
        {
            EnvironmentPolicy = Policy("missing-provider"),
            RuntimeNodeCapabilities = new RuntimeNodeCapabilitiesArtifact
            {
                SupportedProviderKeys = ["missing-provider"],
                SupportedExecutionModes = [ExecutionConstants.SandboxMode]
            }
        });

        var result = resolver.Resolve(Request(TaskArtifactFor("task.missing-provider")));

        Assert.False(result.Succeeded);
        Assert.Equal("ExecutionProviderNotConfigured", result.ErrorCode);
    }

    [Fact]
    public void ResolveRejectsProviderExecutionModeUnsupportedByRuntimeNode()
    {
        var resolver = CreateResolver(new RuntimeExecutionOptions
        {
            EnvironmentPolicy = Policy("kubernetes"),
            RuntimeNodeCapabilities = new RuntimeNodeCapabilitiesArtifact
            {
                SupportedProviderKeys = ["kubernetes"],
                SupportedExecutionModes = [ExecutionConstants.RemoteWorkerMode]
            }
        });

        var result = resolver.Resolve(Request(TaskArtifactFor("task.unsupported-mode")));

        Assert.False(result.Succeeded);
        Assert.Equal("ExecutionModeNotSupported", result.ErrorCode);
    }

    [Fact]
    public void ResolveRejectsExecutionModeDisallowedByTask()
    {
        var task = TaskArtifactFor("task.disallowed-mode") with
        {
            RuntimeRequirements = new ExecutionRuntimeRequirementsArtifact
            {
                AllowedExecutionModes = [ExecutionConstants.RemoteWorkerMode]
            }
        };
        var resolver = CreateResolver(new RuntimeExecutionOptions
        {
            RuntimeNodeCapabilities = RuntimeNodeCapabilitiesArtifact.LocalDefaults
        });

        var result = resolver.Resolve(Request(task));

        Assert.False(result.Succeeded);
        Assert.Equal("ExecutionModeNotAllowed", result.ErrorCode);
    }

    [Fact]
    public void ResolveRejectsNetworkRequirementWhenRuntimeNodeCannotProvideNetwork()
    {
        var task = TaskArtifactFor("task.network") with
        {
            RuntimeRequirements = new ExecutionRuntimeRequirementsArtifact
            {
                RequiresNetwork = true
            }
        };
        var resolver = CreateResolver(new RuntimeExecutionOptions
        {
            RuntimeNodeCapabilities = new RuntimeNodeCapabilitiesArtifact
            {
                SupportedProviderKeys = [ExecutionConstants.BuiltInLocalProvider],
                SupportedExecutionModes = [ExecutionConstants.InProcessTrustedMode],
                SupportsNetwork = false
            }
        });

        var result = resolver.Resolve(Request(task));

        Assert.False(result.Succeeded);
        Assert.Equal("ExecutionNetworkUnavailable", result.ErrorCode);
    }

    [Fact]
    public void ResolveRejectsMemoryRequirementAboveRuntimeNodeLimit()
    {
        var task = TaskArtifactFor("task.memory") with
        {
            RuntimeRequirements = new ExecutionRuntimeRequirementsArtifact
            {
                MinMemoryMb = 512
            }
        };
        var resolver = CreateResolver(new RuntimeExecutionOptions
        {
            RuntimeNodeCapabilities = new RuntimeNodeCapabilitiesArtifact
            {
                SupportedProviderKeys = [ExecutionConstants.BuiltInLocalProvider],
                SupportedExecutionModes = [ExecutionConstants.InProcessTrustedMode],
                MaxMemoryMb = 128
            }
        });

        var result = resolver.Resolve(Request(task));

        Assert.False(result.Succeeded);
        Assert.Equal("ExecutionMemoryUnavailable", result.ErrorCode);
    }

    [Fact]
    public void ResolveClampsPolicyMemoryToTaskMaximumAndPromotesIsolationRequirement()
    {
        var task = TaskArtifactFor("task.memory-clamp") with
        {
            ExecutionPolicy = new ExecutionPolicyArtifact
            {
                DefaultProviderKey = "kubernetes",
                MemoryMb = 512
            },
            RuntimeRequirements = new ExecutionRuntimeRequirementsArtifact
            {
                MaxMemoryMb = 128,
                Isolation = ExecutionIsolationRequirement.Required
            }
        };
        var resolver = CreateResolver(new RuntimeExecutionOptions
        {
            RuntimeNodeCapabilities = new RuntimeNodeCapabilitiesArtifact
            {
                SupportedProviderKeys = ["kubernetes"],
                SupportedExecutionModes = [ExecutionConstants.SandboxMode],
                MaxMemoryMb = 256
            }
        });

        var result = resolver.Resolve(Request(task));

        Assert.True(result.Succeeded);
        Assert.Equal(128, result.ResolvedPolicy.MemoryMb);
        Assert.Equal(ExecutionIsolationRequirement.Required, result.ResolvedPolicy.Isolation);
    }

    [Fact]
    public void ResolveSelectsFirstAllowedProviderWhenNoDefaultProviderIsConfigured()
    {
        var resolver = CreateResolver(new RuntimeExecutionOptions
        {
            EnvironmentPolicy = ExecutionPolicyArtifact.Empty,
            RuntimeNodeCapabilities = new RuntimeNodeCapabilitiesArtifact
            {
                SupportedProviderKeys = ["remote-worker", "kubernetes"],
                SupportedExecutionModes = [ExecutionConstants.RemoteWorkerMode, ExecutionConstants.SandboxMode]
            }
        });

        var result = resolver.Resolve(Request(TaskArtifactFor("task.first-provider")));

        Assert.True(result.Succeeded);
        Assert.Equal("kubernetes", result.ResolvedPolicy.ProviderKey);
        Assert.Equal(ExecutionPolicyScope.Environment, result.ResolvedPolicy.ProviderSource);
    }

    [Fact]
    public void ResolveUsesAllowedProviderWhenRuntimeCapabilitiesDoNotDeclareProviderInventory()
    {
        var resolver = CreateResolver(new RuntimeExecutionOptions
        {
            EnvironmentPolicy = new ExecutionPolicyArtifact
            {
                AllowedProviderKeys = ["kubernetes"],
                ForbiddenProviderKeys = null!,
                Isolation = ExecutionIsolationRequirement.Recommended,
                TimeoutSeconds = 45
            },
            RuntimeNodeCapabilities = new RuntimeNodeCapabilitiesArtifact
            {
                SupportedExecutionModes = [ExecutionConstants.SandboxMode],
                SupportsNetwork = true
            }
        });

        var result = resolver.Resolve(Request(TaskArtifactFor("task.allowed-provider")));

        Assert.True(result.Succeeded);
        Assert.Equal("kubernetes", result.ResolvedPolicy.ProviderKey);
        Assert.Equal(ExecutionIsolationRequirement.Recommended, result.ResolvedPolicy.Isolation);
        Assert.Equal(45, result.ResolvedPolicy.TimeoutSeconds);
    }

    private static IExecutionPolicyResolver CreateResolver(RuntimeExecutionOptions options)
        => new DefaultExecutionPolicyResolver(
            Options.Create(options),
            new ExecutionSandboxProviderRegistry(
            [
                new TestExecutionSandboxProvider(
                    ExecutionConstants.BuiltInLocalProvider,
                    ExecutionConstants.InProcessTrustedMode,
                    isSandbox: false),
                new TestExecutionSandboxProvider(
                    "kubernetes",
                    ExecutionConstants.SandboxMode,
                    isSandbox: true),
                new TestExecutionSandboxProvider(
                    "remote-worker",
                    ExecutionConstants.RemoteWorkerMode,
                    isSandbox: true)
            ]));

    private static ExecutionPolicyResolutionRequest Request(
        TaskArtifact task,
        ExecutionPolicyArtifact? orchestrationPolicy = null,
        ExecutionPolicyArtifact? stagePolicy = null)
        => new()
        {
            StageKey = "stage-one",
            Task = task,
            OrchestrationPolicy = orchestrationPolicy ?? ExecutionPolicyArtifact.Empty,
            StagePolicy = stagePolicy ?? ExecutionPolicyArtifact.Empty
        };

    private static ExecutionPolicyArtifact Policy(string provider)
        => new()
        {
            DefaultProviderKey = provider
        };

    private static TaskArtifact TaskArtifactFor(string key)
        => new(
            Id.New(),
            key,
            key,
            1,
            string.Empty,
            TaskKind.Messaging,
            TaskExecutionMode.Sequential,
            null,
            new ExecutionConditionArtifact(
                EngineType.DSL,
                new DslConditionConfigurationArtifact(new Expression("true"))),
            new TransformationArtifact(
                EngineType.DSL,
                new DslTransformationConfigurationArtifact()),
            new MessagingTaskConfigurationArtifact(key, Version, null!),
            null,
            null,
            OnErrorPolicy.Stop,
            null,
            TaskDispatchType.FireAndWaitCallback,
            true);

    private sealed class TestExecutionSandboxProvider : IExecutionSandboxProvider
    {
        public TestExecutionSandboxProvider(string providerKey, string executionMode, bool isSandbox)
        {
            ProviderKey = providerKey;
            ExecutionMode = executionMode;
            IsSandbox = isSandbox;
        }

        public string ProviderKey { get; }

        public string ExecutionMode { get; }

        public bool IsSandbox { get; }

        public System.Threading.Tasks.Task DispatchAsync(ExecutionEnvelope envelope, CancellationToken cancellationToken = default)
            => System.Threading.Tasks.Task.CompletedTask;
    }
}
