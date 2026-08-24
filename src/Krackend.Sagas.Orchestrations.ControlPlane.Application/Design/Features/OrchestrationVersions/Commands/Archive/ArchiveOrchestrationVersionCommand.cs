using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Represents archive orchestration version command.
/// </summary>
public sealed record ArchiveOrchestrationVersionCommand(string Id, string UpdatedBy) : IRequest<bool>;


