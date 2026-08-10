namespace Krackend.Sagas.Orchestrations.Design.Storage;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Design.Core;

/// <summary>
/// Defines command-oriented persistence operations for variable definitions.
/// </summary>
public interface IVariableDefinitionRepository
{
    /// <summary>
    /// Persists a new variable definition.
    /// </summary>
    /// <param name="variableDefinition">Variable definition to create.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    Task Create(VariableDefinition variableDefinition, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists changes to an existing variable definition.
    /// </summary>
    /// <param name="variableDefinition">Variable definition with updated values.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    Task Update(VariableDefinition variableDefinition, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a variable definition by its identifier.
    /// </summary>
    /// <param name="variableDefinitionId">Identifier of the variable definition to delete.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    Task Delete(Id variableDefinitionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all variable definitions that belong to one orchestration version.
    /// </summary>
    /// <param name="orchestrationVersionId">Parent orchestration version identifier.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>List of variable definitions for the given orchestration version.</returns>
    Task<IEnumerable<VariableDefinition>> GetAll(Id orchestrationVersionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns one variable definition by its identifier.
    /// </summary>
    /// <param name="variableDefinitionId">Identifier of the variable definition.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>The variable definition that matches the identifier.</returns>
    Task<VariableDefinition> GetById(Id variableDefinitionId, CancellationToken cancellationToken = default);
}

