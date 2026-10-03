namespace Krackend.Sagas.Orchestrations.Runtime.Execution;

using Krackend.Sagas.Orchestrations.Abstractions.Execution;

/// <summary>
/// Configures runtime execution policies and node capabilities.
/// </summary>
public sealed class RuntimeExecutionOptions
{
    /// <summary>
    /// Gets or sets environment-level execution policy defaults and constraints.
    /// </summary>
    public ExecutionPolicyArtifact EnvironmentPolicy { get; set; } = new()
    {
        DefaultProviderKey = ExecutionConstants.BuiltInLocalProvider
    };

    /// <summary>
    /// Gets or sets runtime-node execution policy defaults and constraints.
    /// </summary>
    public ExecutionPolicyArtifact RuntimeNodePolicy { get; set; } = ExecutionPolicyArtifact.Empty;

    /// <summary>
    /// Gets or sets runtime-node capabilities.
    /// </summary>
    public RuntimeNodeCapabilitiesArtifact RuntimeNodeCapabilities { get; set; } =
        RuntimeNodeCapabilitiesArtifact.LocalDefaults;
}

