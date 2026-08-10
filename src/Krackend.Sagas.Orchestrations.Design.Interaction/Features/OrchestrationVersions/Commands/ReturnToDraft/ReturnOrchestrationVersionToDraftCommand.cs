using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.Design.Interaction;

/// <summary>
/// Represents return orchestration version to draft command.
/// </summary>
public sealed record ReturnOrchestrationVersionToDraftCommand(string Id) : IRequest<bool>;


