using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.Design.Interaction;

/// <summary>
/// Represents reopen orchestration version review command.
/// </summary>
public sealed record ReopenOrchestrationVersionReviewCommand(string Id, string UpdatedBy) : IRequest<bool>;


