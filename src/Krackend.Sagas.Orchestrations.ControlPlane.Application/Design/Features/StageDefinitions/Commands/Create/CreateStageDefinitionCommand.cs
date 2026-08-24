using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;
using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Represents create stage definition command.
/// </summary>
public sealed record CreateStageDefinitionCommand(
    string OrchestrationVersionId,
    string Key,
    string Name,
    string Description,
    int Order,
    ExecutionCondition ExecutionCondition) : IRequest<string>;

