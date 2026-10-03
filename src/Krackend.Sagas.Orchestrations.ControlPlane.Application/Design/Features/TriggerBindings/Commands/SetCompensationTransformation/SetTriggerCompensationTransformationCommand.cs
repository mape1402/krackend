using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;
using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Represents set trigger compensation transformation command.
/// </summary>
public sealed record SetTriggerCompensationTransformationCommand(
    string Id,
    TransformationDefinition Transformation) : IRequest<bool>;

