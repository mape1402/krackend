using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;
using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Represents set task execution condition command.
/// </summary>
public sealed record SetTaskExecutionConditionCommand(
    string Id,
    ExecutionCondition ExecutionCondition) : IRequest<bool>
{
    /// <summary>Gets the expected persisted designer revision, when atomic saving is required.</summary>
    public string ExpectedDesignRevision { get; init; } = string.Empty;
}
