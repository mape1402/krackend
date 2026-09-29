using global::ButterMorph.Web.Razor;
using Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

namespace Krackend.Sagas.Orchestrations.ControlPlane.WebUI.Design.ButterMorph;

/// <summary>
/// Creates human-readable ButterMorph source metadata from orchestration schema bindings.
/// </summary>
public sealed class OrchestrationButterMorphSourceMetadataFactory : IOrchestrationButterMorphSourceMetadataFactory
{
    /// <inheritdoc />
    public IReadOnlyDictionary<string, ButterMorphDesignerSourceMetadata> Create(OrchestrationSchemaContext schemaContext)
    {
        if (schemaContext?.Sources is null || schemaContext.Sources.Count == 0)
        {
            return new Dictionary<string, ButterMorphDesignerSourceMetadata>(StringComparer.Ordinal);
        }

        return schemaContext.Sources.ToDictionary(
            source => source.Alias,
            Create,
            StringComparer.Ordinal);
    }

    private static ButterMorphDesignerSourceMetadata Create(OrchestrationSchemaSource source)
    {
        return new ButterMorphDesignerSourceMetadata
        {
            DisplayName = BuildDisplayName(source),
            Description = BuildDescription(source),
            Tags = BuildTags(source),
        };
    }

    private static string BuildDisplayName(OrchestrationSchemaSource source)
        => source.SourceKind switch
        {
            OrchestrationSchemaContextSourceKind.Trigger => "Trigger event",
            OrchestrationSchemaContextSourceKind.TriggerMetadata => "Trigger Metadata",
            OrchestrationSchemaContextSourceKind.Metadata => BuildMetadataDisplayName(source),
            OrchestrationSchemaContextSourceKind.TaskRequest => $"{source.TaskKey} request",
            OrchestrationSchemaContextSourceKind.TaskResponse => $"{source.TaskKey} reply",
            _ => source.Alias
        };

    private static string BuildDescription(OrchestrationSchemaSource source)
        => source.SourceKind switch
        {
            OrchestrationSchemaContextSourceKind.Trigger => "Initial orchestration event payload.",
            OrchestrationSchemaContextSourceKind.TriggerMetadata => "Krackend-defined metadata for the event that starts the orchestration.",
            OrchestrationSchemaContextSourceKind.Metadata => "Transversal metadata propagated with the orchestration message.",
            OrchestrationSchemaContextSourceKind.TaskRequest => $"Request payload sent to task '{source.TaskKey}' in stage '{source.StageKey}'.",
            OrchestrationSchemaContextSourceKind.TaskResponse => $"Reply payload received from task '{source.TaskKey}' in stage '{source.StageKey}'.",
            _ => string.Empty
        };

    private static string BuildMetadataDisplayName(OrchestrationSchemaSource source)
    {
        var contractKey = source.SchemaBinding?.ContractKey;
        return string.IsNullOrWhiteSpace(contractKey)
            ? source.Alias
            : contractKey;
    }

    private static IReadOnlyList<string> BuildTags(OrchestrationSchemaSource source)
    {
        var tags = new List<string>
        {
            source.SourceKind.ToString()
        };

        if (!string.IsNullOrWhiteSpace(source.StageKey))
        {
            tags.Add(source.StageKey);
        }

        if (!string.IsNullOrWhiteSpace(source.TaskKey))
        {
            tags.Add(source.TaskKey);
        }

        return tags;
    }

}
