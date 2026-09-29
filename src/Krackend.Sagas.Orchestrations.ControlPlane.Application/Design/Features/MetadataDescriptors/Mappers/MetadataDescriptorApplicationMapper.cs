using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Maps metadata descriptors to interaction models.
/// </summary>
public sealed class MetadataDescriptorApplicationMapper : IMetadataDescriptorApplicationMapper
{
    /// <inheritdoc />
    public MetadataDescriptorModel ToModel(MetadataDescriptor source)
    {
        return new MetadataDescriptorModel
        {
            Id = source.Id.ToString(),
            Key = source.Key,
            SourceKey = source.SourceKey,
            DisplayName = source.DisplayName,
            Description = source.Description ?? string.Empty,
            SchemaJson = source.SchemaJson,
            ContentHash = source.ContentHash,
        };
    }

    /// <inheritdoc />
    public ApplicationPagedResult<MetadataDescriptorModel> ToPagedModel(PagedResult<MetadataDescriptor> source)
    {
        return new ApplicationPagedResult<MetadataDescriptorModel>(
            source.PageNumber,
            source.TotalPages,
            source.TotalRows,
            source.PageSize,
            source.Rows.Select(ToModel));
    }
}
