namespace Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;

/// <summary>
/// Defines command-oriented persistence operations for branch rules.
/// </summary>
public interface IBranchRuleRepository
{
    /// <summary>
    /// Persists a new branch rule.
    /// </summary>
    /// <param name="branchRuleDefinition">Branch rule definition to create.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    Task Create(BranchRuleDefinition branchRuleDefinition, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists changes to an existing branch rule.
    /// </summary>
    /// <param name="branchRuleDefinition">Branch rule definition with updated values.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    Task Update(BranchRuleDefinition branchRuleDefinition, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a branch rule by its identifier.
    /// </summary>
    /// <param name="branchRuleDefinitionId">Identifier of the branch rule definition to delete.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    Task Delete(Id branchRuleDefinitionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all branch rules that belong to one stage definition.
    /// </summary>
    /// <param name="stageDefinitionId">Parent stage definition identifier.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>List of branch rules for the given stage definition.</returns>
    Task<IEnumerable<BranchRuleDefinition>> GetAll(Id stageDefinitionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns one branch rule by its identifier.
    /// </summary>
    /// <param name="branchRuleDefinitionId">Identifier of the branch rule definition.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>The branch rule that matches the identifier.</returns>
    Task<BranchRuleDefinition> GetById(Id branchRuleDefinitionId, CancellationToken cancellationToken = default);
}

