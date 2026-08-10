using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.Design.Interaction;

/// <summary>
/// Represents create orchestration version command.
/// </summary>
public sealed record CreateOrchestrationVersionCommand(
    string OrchestrationDefinitionId,
    string Version,
    OrchestrationVersionStatus Status,
    string VersionLabel,
    string Description,
    string Checksum,
    string Notes,
    string CreatedBy) : IRequest<string>;


