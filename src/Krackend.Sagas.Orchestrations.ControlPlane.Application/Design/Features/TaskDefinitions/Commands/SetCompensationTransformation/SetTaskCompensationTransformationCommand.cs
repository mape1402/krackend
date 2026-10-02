using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;
using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Represents set task compensation transformation command.
/// </summary>
public sealed record SetTaskCompensationTransformationCommand(
    string Id,
    TransformationDefinition Transformation) : IRequest<bool>;

