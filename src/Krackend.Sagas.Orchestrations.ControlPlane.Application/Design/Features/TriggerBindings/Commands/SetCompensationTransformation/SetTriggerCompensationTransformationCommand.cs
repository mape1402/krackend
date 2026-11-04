using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;
using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Represents set trigger compensation transformation command.
/// </summary>
public sealed record SetTriggerCompensationTransformationCommand(
    string Id,
    TransformationDefinition Transformation) : IRequest<bool>
{
    /// <summary>Gets the expected persisted designer revision, when atomic saving is required.</summary>
    public string ExpectedDesignRevision { get; init; } = string.Empty;
}

