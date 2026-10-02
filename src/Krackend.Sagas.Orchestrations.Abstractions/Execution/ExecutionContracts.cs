namespace Krackend.Sagas.Orchestrations.Abstractions.Execution;

/// <summary>
/// Well-known execution provider and mode keys.
/// </summary>
public static class ExecutionConstants
{
    /// <summary>
    /// Built-in local provider that dispatches through the configured in-process runtime adapters.
    /// </summary>
    public const string BuiltInLocalProvider = "built-in-local";

    /// <summary>
    /// Execution mode for trusted in-process/built-in work.
    /// </summary>
    public const string InProcessTrustedMode = "in-process-trusted";

    /// <summary>
    /// Execution mode for externally isolated sandbox work.
    /// </summary>
    public const string SandboxMode = "sandbox";

    /// <summary>
    /// Execution mode for remote worker execution.
    /// </summary>
    public const string RemoteWorkerMode = "remote-worker";
}

/// <summary>
/// Describes how much isolation a capability requires.
/// </summary>
public enum ExecutionIsolationRequirement
{
    /// <summary>
    /// No external isolation is required.
    /// </summary>
    None = 0,

    /// <summary>
    /// Isolation is preferred when available.
    /// </summary>
    Recommended = 1,

    /// <summary>
    /// Isolation is mandatory.
    /// </summary>
    Required = 2
}

/// <summary>
/// Identifies the scope that contributed an execution policy.
/// </summary>
public enum ExecutionPolicyScope
{
    /// <summary>
    /// Environment-level runtime defaults and constraints.
    /// </summary>
    Environment = 0,

    /// <summary>
    /// Runtime-node specific defaults and constraints.
    /// </summary>
    RuntimeNode = 1,

    /// <summary>
    /// Orchestration artifact defaults and constraints.
    /// </summary>
    Orchestration = 2,

    /// <summary>
    /// Stage-level defaults and constraints.
    /// </summary>
    Stage = 3,

    /// <summary>
    /// Task-level defaults and constraints.
    /// </summary>
    Task = 4
}

/// <summary>
/// Declares the runtime requirements of a capability or task.
/// </summary>
public sealed record ExecutionRuntimeRequirementsArtifact
{
    /// <summary>
    /// Gets an empty requirements instance.
    /// </summary>
    public static ExecutionRuntimeRequirementsArtifact Empty { get; } = new();

    /// <summary>
    /// Gets the required isolation boundary.
    /// </summary>
    public ExecutionIsolationRequirement Isolation { get; init; } = ExecutionIsolationRequirement.None;

    /// <summary>
    /// Gets the minimum memory required by the execution.
    /// </summary>
    public int? MinMemoryMb { get; init; }

    /// <summary>
    /// Gets the maximum memory requested by the execution.
    /// </summary>
    public int? MaxMemoryMb { get; init; }

    /// <summary>
    /// Gets whether outbound network access is required.
    /// </summary>
    public bool RequiresNetwork { get; init; }

    /// <summary>
    /// Gets the secrets required by the execution.
    /// </summary>
    public IReadOnlyList<string> RequiredSecrets { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets allowed execution modes. Empty means any mode allowed by policy and runtime capabilities.
    /// </summary>
    public IReadOnlyList<string> AllowedExecutionModes { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Defines execution defaults and hard constraints for a scope.
/// </summary>
public sealed record ExecutionPolicyArtifact
{
    /// <summary>
    /// Gets an empty execution policy.
    /// </summary>
    public static ExecutionPolicyArtifact Empty { get; } = new();

    /// <summary>
    /// Gets the default provider requested by this scope.
    /// </summary>
    public string DefaultProviderKey { get; init; } = string.Empty;

    /// <summary>
    /// Gets provider keys allowed by this scope. Empty means no allow-list constraint.
    /// </summary>
    public IReadOnlyList<string> AllowedProviderKeys { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets provider keys forbidden by this scope.
    /// </summary>
    public IReadOnlyList<string> ForbiddenProviderKeys { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets whether external extensions must execute in a sandbox from this scope downward.
    /// </summary>
    public bool? RequireSandboxForExternalExtensions { get; init; }

    /// <summary>
    /// Gets the isolation default or constraint contributed by this scope.
    /// </summary>
    public ExecutionIsolationRequirement? Isolation { get; init; }

    /// <summary>
    /// Gets the execution timeout in seconds requested by this scope.
    /// </summary>
    public int? TimeoutSeconds { get; init; }

    /// <summary>
    /// Gets memory in megabytes requested by this scope.
    /// </summary>
    public int? MemoryMb { get; init; }
}

/// <summary>
/// Describes providers and resources available on a runtime node.
/// </summary>
public sealed record RuntimeNodeCapabilitiesArtifact
{
    /// <summary>
    /// Gets default local capabilities.
    /// </summary>
    public static RuntimeNodeCapabilitiesArtifact LocalDefaults { get; } = new()
    {
        SupportedProviderKeys = new[] { ExecutionConstants.BuiltInLocalProvider },
        SupportedExecutionModes = new[] { ExecutionConstants.InProcessTrustedMode },
        SupportsNetwork = true
    };

    /// <summary>
    /// Gets providers supported by this runtime node.
    /// </summary>
    public IReadOnlyList<string> SupportedProviderKeys { get; init; } =
        Array.Empty<string>();

    /// <summary>
    /// Gets execution modes supported by this runtime node.
    /// </summary>
    public IReadOnlyList<string> SupportedExecutionModes { get; init; } =
        Array.Empty<string>();

    /// <summary>
    /// Gets whether network-capable execution is available.
    /// </summary>
    public bool SupportsNetwork { get; init; }

    /// <summary>
    /// Gets the maximum memory this runtime node can assign to one execution.
    /// </summary>
    public int? MaxMemoryMb { get; init; }

    /// <summary>
    /// Gets secret keys/bindings this runtime node can resolve. Empty means not constrained by this descriptor.
    /// </summary>
    public IReadOnlyList<string> AvailableSecrets { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Captures the policy selected for a concrete execution attempt.
/// </summary>
public sealed record ResolvedExecutionPolicyArtifact(
    string ProviderKey,
    string ExecutionMode,
    ExecutionPolicyScope ProviderSource,
    ExecutionIsolationRequirement Isolation)
{
    /// <summary>
    /// Gets the resolved execution timeout in seconds.
    /// </summary>
    public int? TimeoutSeconds { get; init; }

    /// <summary>
    /// Gets the resolved memory limit/request in megabytes.
    /// </summary>
    public int? MemoryMb { get; init; }

    /// <summary>
    /// Gets whether external extensions are required to run in a sandbox by the resolved policy.
    /// </summary>
    public bool RequireSandboxForExternalExtensions { get; init; }
}

