namespace Krackend.Sagas.Orchestrations.Abstractions.Extensions;

/// <summary>
/// Well-known extension constants used by the orchestration contract model.
/// </summary>
public static class ExtensionConstants
{
    /// <summary>
    /// Built-in extension key used for capabilities shipped with Krackend.
    /// </summary>
    public const string BuiltInExtensionKey = "krackend.built-in";
}

/// <summary>
/// Identifies an extension package.
/// </summary>
public readonly record struct ExtensionKey(string Value)
{
    /// <inheritdoc />
    public override string ToString() => Value ?? string.Empty;
}

/// <summary>
/// Identifies a capability exposed by an extension.
/// </summary>
public readonly record struct CapabilityKey(string Value)
{
    /// <inheritdoc />
    public override string ToString() => Value ?? string.Empty;
}

/// <summary>
/// Identifies a task type exposed by an extension.
/// </summary>
public readonly record struct TaskTypeKey(string Value)
{
    /// <inheritdoc />
    public override string ToString() => Value ?? string.Empty;
}

/// <summary>
/// Identifies a trigger type exposed by an extension.
/// </summary>
public readonly record struct TriggerTypeKey(string Value)
{
    /// <inheritdoc />
    public override string ToString() => Value ?? string.Empty;
}

/// <summary>
/// Identifies a control-flow node type exposed by an extension.
/// </summary>
public readonly record struct NodeTypeKey(string Value)
{
    /// <inheritdoc />
    public override string ToString() => Value ?? string.Empty;
}

/// <summary>
/// Identifies a stored extension bundle.
/// </summary>
public readonly record struct ExtensionBundleId(string Value)
{
    /// <inheritdoc />
    public override string ToString() => Value ?? string.Empty;
}

