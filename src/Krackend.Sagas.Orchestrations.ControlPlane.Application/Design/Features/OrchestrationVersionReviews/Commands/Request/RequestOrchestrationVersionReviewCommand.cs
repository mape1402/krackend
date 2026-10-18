namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;
using Pelican.Mediator;

/// <summary>
/// Requests a guarded lifecycle transition for an orchestration version.
/// </summary>
public sealed record RequestOrchestrationVersionReviewCommand(
    string OrchestrationVersionId,
    OrchestrationVersionReviewOperation Operation,
    string RequestedBy) : IRequest<string>;
