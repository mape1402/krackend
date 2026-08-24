namespace Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;

/// <summary>
/// Defines command-oriented persistence operations for parallel group definitions.
/// </summary>
public interface IParallelGroupRepository
{
    /// <summary>
    /// Persists a new parallel group definition.
    /// </summary>
    /// <param name="parallelGroupDefinition">Parallel group definition to create.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    Task Create(ParallelGroupDefinition parallelGroupDefinition, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists changes to an existing parallel group definition.
    /// </summary>
    /// <param name="parallelGroupDefinition">Parallel group definition with updated values.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    Task Update(ParallelGroupDefinition parallelGroupDefinition, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a parallel group definition by its identifier.
    /// </summary>
    /// <param name="parallelGroupDefinitionId">Identifier of the parallel group definition to delete.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    Task Delete(Id parallelGroupDefinitionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all parallel groups that belong to one stage definition.
    /// </summary>
    /// <param name="stageDefinitionId">Parent stage definition identifier.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>List of parallel group definitions for the given stage definition.</returns>
    Task<IEnumerable<ParallelGroupDefinition>> GetAll(Id stageDefinitionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns one parallel group definition by its identifier.
    /// </summary>
    /// <param name="parallelGroupDefinitionId">Identifier of the parallel group definition.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>The parallel group definition that matches the identifier.</returns>
    Task<ParallelGroupDefinition> GetById(Id parallelGroupDefinitionId, CancellationToken cancellationToken = default);
}

