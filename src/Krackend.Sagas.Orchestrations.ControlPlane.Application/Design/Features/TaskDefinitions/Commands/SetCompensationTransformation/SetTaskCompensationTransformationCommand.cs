using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;
using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Represents set task compensation transformation command.
/// </summary>
public sealed record SetTaskCompensationTransformationCommand(
    string Id,
    TransformationDefinition Transformation) : IRequest<bool>
{
    /// <summary>Gets the expected persisted designer revision, when atomic saving is required.</summary>
    public string ExpectedDesignRevision { get; init; } = string.Empty;
}

