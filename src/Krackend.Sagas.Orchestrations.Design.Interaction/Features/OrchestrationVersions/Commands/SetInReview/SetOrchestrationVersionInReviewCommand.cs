using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.Design.Interaction;

/// <summary>
/// Represents set orchestration version in review command.
/// </summary>
public sealed record SetOrchestrationVersionInReviewCommand(string Id) : IRequest<bool>;


