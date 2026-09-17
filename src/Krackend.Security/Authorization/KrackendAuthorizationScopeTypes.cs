namespace Krackend.Security.Authorization;

/// <summary>
/// Defines stable authorization scope type names.
/// </summary>
public static class KrackendAuthorizationScopeTypes
{
    /// <summary>
    /// Applies to every product area.
    /// </summary>
    public const string Global = "Global";

    /// <summary>
    /// Applies to the control-plane area.
    /// </summary>
    public const string ControlPlane = "ControlPlane";

    /// <summary>
    /// Applies to the runtime area.
    /// </summary>
    public const string Runtime = "Runtime";

    /// <summary>
    /// Applies to a runtime node.
    /// </summary>
    public const string RuntimeNode = "RuntimeNode";
}
