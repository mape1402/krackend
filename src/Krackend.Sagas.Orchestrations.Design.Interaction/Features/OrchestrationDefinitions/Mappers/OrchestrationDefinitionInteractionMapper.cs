using Krackend.Sagas.Orchestrations.Design.Core;
using Krackend.Sagas.Orchestrations.Design.Storage;

namespace Krackend.Sagas.Orchestrations.Design.Interaction;

/// <summary>
/// Maps domain definitions to interaction models.
/// </summary>
public sealed class OrchestrationDefinitionInteractionMapper : IOrchestrationDefinitionInteractionMapper
{
    /// <summary>
    /// Maps a domain definition to an interaction model.
    /// </summary>
    /// <param name="source">Source value to map.</param>
    /// <returns>Operation result.</returns>
    public OrchestrationDefinitionModel ToModel(OrchestrationDefinition source)
    {
        return new OrchestrationDefinitionModel
        {
            Id = source.Id.ToString(),
            Key = source.Key,
            Name = source.Name,
            Description = source.Description ?? string.Empty,
            Domain = source.Domain ?? string.Empty,
            DomainId = source.DomainId?.ToString() ?? string.Empty,
            DomainDisplayName = string.IsNullOrWhiteSpace(source.DomainDisplayName)
                ? source.Domain ?? string.Empty
                : source.DomainDisplayName,
            OwnerTeam = source.OwnerTeam ?? string.Empty,
            OwnerTeamId = source.OwnerTeamId?.ToString() ?? string.Empty,
            OwnerTeamDisplayName = string.IsNullOrWhiteSpace(source.OwnerTeamDisplayName)
                ? source.OwnerTeam ?? string.Empty
                : source.OwnerTeamDisplayName,
            Tags = source.Tags,
            IsActive = source.IsActive,
            CreatedOnUtc = source.CreatedOnUtc.ToString("O"),
            CreatedBy = source.CreatedBy,
            UpdatedOnUtc = PrimitiveParser.FormatUtc(source.UpdatedOnUtc),
            UpdatedBy = source.UpdatedBy ?? string.Empty,
        };
    }

    /// <summary>
    /// Maps a paged domain result to an interaction paged result.
    /// </summary>
    /// <param name="source">Source value to map.</param>
    /// <returns>Paged interaction result.</returns>
    public InteractionPagedResult<OrchestrationDefinitionModel> ToPagedModel(PagedResult<OrchestrationDefinition> source)
    {
        return new InteractionPagedResult<OrchestrationDefinitionModel>(
            source.PageNumber,
            source.TotalPages,
            source.TotalRows,
            source.PageSize,
            source.Rows.Select(ToModel));
    }
}


