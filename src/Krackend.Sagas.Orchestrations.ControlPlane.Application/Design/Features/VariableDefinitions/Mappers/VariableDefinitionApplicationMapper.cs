using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Maps domain definitions to interaction models.
/// </summary>
public sealed class VariableDefinitionApplicationMapper : IVariableDefinitionApplicationMapper
{
    /// <summary>
    /// Maps a domain definition to an interaction model.
    /// </summary>
    /// <param name="source">Source value to map.</param>
    /// <returns>Operation result.</returns>
    public VariableDefinitionModel ToModel(VariableDefinition source)
    {
        return new VariableDefinitionModel
        {
            Id = source.Id.ToString(),
            OrchestrationVersionId = source.OrchestrationVersionId.ToString(),
            Key = source.Key,
            DisplayName = source.DisplayName ?? string.Empty,
            Description = source.Description ?? string.Empty,
            Scope = source.Scope,
            ValueType = source.ValueType,
            DefaultValue = source.DefaultValue ?? string.Empty,
            IsRequired = source.IsRequired,
            IsSensitive = source.IsSensitive,
        };
    }
}

