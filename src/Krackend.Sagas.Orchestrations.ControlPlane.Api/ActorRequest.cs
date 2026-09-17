namespace Krackend.Sagas.Orchestrations.ControlPlane.Api;

/// <summary>
/// Represents the actor that initiated an API operation.
/// </summary>
public sealed class ActorRequest
{
    /// <summary>
    /// Gets or sets the actor identifier.
    /// </summary>
    public string Actor { get; set; } = string.Empty;
}
