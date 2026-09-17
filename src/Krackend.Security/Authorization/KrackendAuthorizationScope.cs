namespace Krackend.Security.Authorization;

/// <summary>
/// Represents the resource scope where a permission is evaluated.
/// </summary>
public readonly record struct KrackendAuthorizationScope(string ScopeType, string ScopeId)
{
    /// <summary>
    /// Creates the global authorization scope.
    /// </summary>
    public static KrackendAuthorizationScope Global()
        => new(KrackendAuthorizationScopeTypes.Global, string.Empty);

    /// <summary>
    /// Creates the control-plane authorization scope.
    /// </summary>
    public static KrackendAuthorizationScope ControlPlane()
        => new(KrackendAuthorizationScopeTypes.ControlPlane, string.Empty);

    /// <summary>
    /// Creates the runtime authorization scope.
    /// </summary>
    public static KrackendAuthorizationScope Runtime()
        => new(KrackendAuthorizationScopeTypes.Runtime, string.Empty);

    /// <summary>
    /// Creates an authorization scope with the supplied type and identifier.
    /// </summary>
    /// <param name="scopeType">Scope type.</param>
    /// <param name="scopeId">Scope identifier.</param>
    /// <returns>Authorization scope.</returns>
    public static KrackendAuthorizationScope Create(string scopeType, string scopeId = "")
        => new(scopeType ?? string.Empty, scopeId ?? string.Empty);
}
