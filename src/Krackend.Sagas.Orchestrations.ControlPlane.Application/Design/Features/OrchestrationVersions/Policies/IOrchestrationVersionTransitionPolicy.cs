using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Defines policy rules for orchestration version transition.
/// </summary>
public interface IOrchestrationVersionTransitionPolicy
{
    /// <summary>
    /// Executes can transition.
    /// </summary>
    /// <param name="from">The from.</param>
    /// <param name="to">The to.</param>
    /// <returns>True when the operation completes successfully.</returns>
    bool CanTransition(OrchestrationVersionStatus from, OrchestrationVersionStatus to);
    /// <summary>
    /// Validates that a status transition is allowed.
    /// </summary>
    /// <param name="from">The from.</param>
    /// <param name="to">The to.</param>
    void EnsureCanTransition(OrchestrationVersionStatus from, OrchestrationVersionStatus to);
    /// <summary>
    /// Executes get allowed targets.
    /// </summary>
    /// <param name="from">The from.</param>
    /// <returns>Collection result.</returns>
    IEnumerable<OrchestrationVersionStatus> GetAllowedTargets(OrchestrationVersionStatus from);
}


