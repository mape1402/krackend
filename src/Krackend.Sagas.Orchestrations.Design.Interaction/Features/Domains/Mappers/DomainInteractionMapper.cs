using Krackend.Sagas.Orchestrations.Design.Core;
using Krackend.Sagas.Orchestrations.Design.Storage;

namespace Krackend.Sagas.Orchestrations.Design.Interaction;

/// <summary>
/// Maps domain entities to interaction models.
/// </summary>
public sealed class DomainInteractionMapper : IDomainInteractionMapper
{
    /// <inheritdoc />
    public DomainModel ToModel(Domain source)
    {
        return new DomainModel
        {
            Id = source.Id.ToString(),
            Key = source.Key,
            DisplayName = source.DisplayName,
            Description = source.Description ?? string.Empty,
            IsActive = source.IsActive,
        };
    }

    /// <inheritdoc />
    public InteractionPagedResult<DomainModel> ToPagedModel(PagedResult<Domain> source)
    {
        return new InteractionPagedResult<DomainModel>(
            source.PageNumber,
            source.TotalPages,
            source.TotalRows,
            source.PageSize,
            source.Rows.Select(ToModel));
    }
}
