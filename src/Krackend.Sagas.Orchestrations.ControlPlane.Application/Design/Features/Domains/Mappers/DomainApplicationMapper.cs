using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Maps domain entities to interaction models.
/// </summary>
public sealed class DomainApplicationMapper : IDomainApplicationMapper
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
    public ApplicationPagedResult<DomainModel> ToPagedModel(PagedResult<Domain> source)
    {
        return new ApplicationPagedResult<DomainModel>(
            source.PageNumber,
            source.TotalPages,
            source.TotalRows,
            source.PageSize,
            source.Rows.Select(ToModel));
    }
}
