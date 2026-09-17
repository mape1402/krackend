namespace Krackend.Sagas.Orchestrations.ControlPlane.Api;

/// <summary>
/// Represents an active state update request.
/// </summary>
public sealed class SetActiveRequest
{
    /// <summary>
    /// Gets or sets a value indicating whether the target resource must be active.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Gets or sets the actor identifier.
    /// </summary>
    public string Actor { get; set; } = "api";
}
