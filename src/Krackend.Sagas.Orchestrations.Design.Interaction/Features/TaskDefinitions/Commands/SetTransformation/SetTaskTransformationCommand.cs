using Krackend.Sagas.Orchestrations.Design.Core;
using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.Design.Interaction;

/// <summary>
/// Represents set task transformation command.
/// </summary>
public sealed record SetTaskTransformationCommand(
    string Id,
    TransformationDefinition Transformation) : IRequest<bool>;

