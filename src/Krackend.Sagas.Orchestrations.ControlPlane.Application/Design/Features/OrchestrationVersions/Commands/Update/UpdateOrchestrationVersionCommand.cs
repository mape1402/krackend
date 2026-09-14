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
    string UpdatedBy) : IRequest<bool>;


