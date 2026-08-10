using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.Design.Interaction;

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


