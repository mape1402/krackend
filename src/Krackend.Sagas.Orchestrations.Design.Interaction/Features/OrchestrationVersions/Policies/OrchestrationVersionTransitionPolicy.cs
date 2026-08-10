using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

namespace Krackend.Sagas.Orchestrations.Design.Interaction;

/// <summary>
/// Implements policy rules for orchestration version transition.
/// </summary>
public sealed class OrchestrationVersionTransitionPolicy : IOrchestrationVersionTransitionPolicy
{
    private static readonly IReadOnlyDictionary<OrchestrationVersionStatus, HashSet<OrchestrationVersionStatus>> Transitions =
        new Dictionary<OrchestrationVersionStatus, HashSet<OrchestrationVersionStatus>>
        {
            [OrchestrationVersionStatus.Draft] = new HashSet<OrchestrationVersionStatus>
            {
                OrchestrationVersionStatus.InReview,
            },
            [OrchestrationVersionStatus.InReview] = new HashSet<OrchestrationVersionStatus>
            {
                OrchestrationVersionStatus.Draft,
                OrchestrationVersionStatus.Approved,
            },
            [OrchestrationVersionStatus.Approved] = new HashSet<OrchestrationVersionStatus>
            {
                OrchestrationVersionStatus.InReview,
                OrchestrationVersionStatus.Deployed,
            },
            [OrchestrationVersionStatus.Deployed] = new HashSet<OrchestrationVersionStatus>
            {
                OrchestrationVersionStatus.Deprecated,
            },
            [OrchestrationVersionStatus.Deprecated] = new HashSet<OrchestrationVersionStatus>
            {
                OrchestrationVersionStatus.Archived,
            },
            [OrchestrationVersionStatus.Archived] = new HashSet<OrchestrationVersionStatus>(),
        };

    /// <summary>
    /// Executes can transition.
    /// </summary>
    /// <param name="from">The from.</param>
    /// <param name="to">The to.</param>
    /// <returns>True when the operation completes successfully.</returns>
    public bool CanTransition(OrchestrationVersionStatus from, OrchestrationVersionStatus to)
    {
        if (!Transitions.TryGetValue(from, out HashSet<OrchestrationVersionStatus> allowed))
        {
            return false;
        }

        return allowed.Contains(to);
    }

    /// <summary>
    /// Validates that a status transition is allowed.
    /// </summary>
    /// <param name="from">The from.</param>
    /// <param name="to">The to.</param>
    public void EnsureCanTransition(OrchestrationVersionStatus from, OrchestrationVersionStatus to)
    {
        if (CanTransition(from, to))
        {
            return;
        }

        string allowed = string.Join(", ", GetAllowedTargets(from));
        throw new InvalidOperationException($"Transition from '{from}' to '{to}' is not allowed. Allowed targets: [{allowed}].");
    }

    /// <summary>
    /// Executes get allowed targets.
    /// </summary>
    /// <param name="from">The from.</param>
    /// <returns>Collection result.</returns>
    public IEnumerable<OrchestrationVersionStatus> GetAllowedTargets(OrchestrationVersionStatus from)
    {
        if (!Transitions.TryGetValue(from, out HashSet<OrchestrationVersionStatus> allowed))
        {
            return Array.Empty<OrchestrationVersionStatus>();
        }

        return allowed;
    }
}


