using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.Design.Interaction;

/// <summary>
/// Represents archive orchestration version command.
/// </summary>
public sealed record ArchiveOrchestrationVersionCommand(string Id, string UpdatedBy) : IRequest<bool>;


