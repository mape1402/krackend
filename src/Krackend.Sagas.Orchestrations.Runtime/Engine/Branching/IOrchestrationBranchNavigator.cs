namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Branching;

/// <summary>
/// Resolves artifact branch rules into runtime navigation decisions.
/// </summary>
public interface IOrchestrationBranchNavigator
{
    /// <summary>
    /// Resolves the branch navigation for a completed orchestration element.
    /// </summary>
    /// <param name="request">Branch navigation request.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The resolved branch navigation result.</returns>
    Task<OrchestrationBranchNavigationResult> ResolveAsync(
        OrchestrationBranchNavigationRequest request,
        CancellationToken cancellationToken = default);
}
