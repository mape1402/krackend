using Krackend.Sagas.Orchestrations.Design.Core;
using Krackend.Sagas.Orchestrations.Design.Storage;

namespace Krackend.Sagas.Orchestrations.Design.Interaction;

/// <summary>
/// Maps domain definitions to interaction models.
/// </summary>
public sealed class OrchestrationVersionInteractionMapper : IOrchestrationVersionInteractionMapper
{
    /// <summary>
    /// Maps a domain definition to an interaction model.
    /// </summary>
    /// <param name="source">Source value to map.</param>
    /// <returns>Operation result.</returns>
    public OrchestrationVersionModel ToModel(OrchestrationVersion source)
    {
        return new OrchestrationVersionModel
        {
            Id = source.Id.ToString(),
            OrchestrationDefinitionId = source.OrchestrationDefinitionId.ToString(),
            Version = source.Version.ToString(),
            Status = source.Status,
            VersionLabel = source.VersionLabel ?? string.Empty,
            Description = source.Description ?? string.Empty,
            Checksum = source.Checksum.ToString(),
            Notes = source.Notes ?? string.Empty,
            TriggerBindingsCount = source.TriggerBindings?.Count ?? 0,
            StageDefinitionsCount = source.StageDefinitions?.Count ?? 0,
            VariableDefinitionsCount = source.VariableDefinitions?.Count ?? 0,
            CreatedOnUtc = source.CreatedOnUtc.ToString("O"),
            CreatedBy = source.CreatedBy,
            ApprovedOnUtc = PrimitiveParser.FormatUtc(source.ApprovedOnUtc),
            ApprovedBy = source.ApprovedBy ?? string.Empty,
            UpdatedOnUtc = PrimitiveParser.FormatUtc(source.UpdatedOnUtc),
            UpdatedBy = source.UpdatedBy ?? string.Empty,
        };
    }

    /// <summary>
    /// Maps a paged domain result to an interaction paged result.
    /// </summary>
    /// <param name="source">Source value to map.</param>
    /// <returns>Paged interaction result.</returns>
    public InteractionPagedResult<OrchestrationVersionModel> ToPagedModel(PagedResult<OrchestrationVersion> source)
    {
        return new InteractionPagedResult<OrchestrationVersionModel>(
            source.PageNumber,
            source.TotalPages,
            source.TotalRows,
            source.PageSize,
            source.Rows.Select(ToModel));
    }
}


