using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Enums;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Api;

/// <summary>
/// Represents a runtime node status update request.
/// </summary>
public sealed class RuntimeNodeStatusRequest
{
    /// <summary>
    /// Gets or sets the next runtime node status.
    /// </summary>
    public RuntimeNodeStatus Status { get; set; }
}
