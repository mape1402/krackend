using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.Design.Interaction;

/// <summary>
/// Represents deprecate orchestration version command.
/// </summary>
public sealed record DeprecateOrchestrationVersionCommand(string Id, string UpdatedBy) : IRequest<bool>;


