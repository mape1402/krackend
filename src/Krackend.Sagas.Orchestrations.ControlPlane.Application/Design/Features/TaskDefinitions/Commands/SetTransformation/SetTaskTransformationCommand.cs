using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;
using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Represents set task transformation command.
/// </summary>
public sealed record SetTaskTransformationCommand(
    string Id,
    TransformationDefinition Transformation) : IRequest<bool>;

