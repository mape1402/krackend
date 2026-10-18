namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

using Pelican.Mediator;

/// <summary>
/// Applies an approved review and creates the corresponding deployed artifact.
/// </summary>
public sealed record ApplyOrchestrationVersionReviewCommand(
    string ReviewId,
    string AppliedBy,
    string Version,
    string VersionLabel,
    string Description,
    string Notes) : IRequest<bool>;
