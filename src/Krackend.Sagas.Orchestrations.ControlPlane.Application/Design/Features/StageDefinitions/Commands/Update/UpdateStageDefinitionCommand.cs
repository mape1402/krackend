using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;
using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Represents update stage definition command.
/// </summary>
public sealed record UpdateStageDefinitionCommand(
    string Id,
    string Key,
    string Name,
    string Description,
    int Order,
    ExecutionCondition ExecutionCondition) : IRequest<bool>;

