using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Represents update orchestration version command.
/// </summary>
public sealed record UpdateOrchestrationVersionCommand(
    string Id,
    string VersionLabel,
    string Description,
    string Checksum,
    string Notes,
    string UpdatedBy) : IRequest<bool>
{
    /// <summary>Indicates that the lifecycle duration settings should be updated.</summary>
    public bool UpdateInstanceDurations { get; init; }
    /// <summary>Maximum lifetime in seconds; null inherits the runtime default and zero disables expiration.</summary>
    public int? MaxInstanceDurationSeconds { get; init; }
    /// <summary>Idle duration in seconds; null inherits the runtime default.</summary>
    public int? IdleDurationSeconds { get; init; }
}


