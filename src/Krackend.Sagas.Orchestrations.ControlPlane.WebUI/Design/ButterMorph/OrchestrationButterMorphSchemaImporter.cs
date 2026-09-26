namespace Krackend.Sagas.Orchestrations.ControlPlane.WebUI.Design.ButterMorph;

using System.Text.Json;
using global::ButterMorph.Abstractions;
using global::ButterMorph.Json.Schema;
using global::ButterMorph.SchemaDesign;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;

/// <summary>
/// Imports schema snapshots into ButterMorph structure schemas.
/// </summary>
public sealed class OrchestrationButterMorphSchemaImporter : IOrchestrationButterMorphSchemaImporter
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IJsonSchemaImporter _jsonSchemaImporter;
    private readonly IPayloadSchemaDefinitionHydrator _payloadSchemaDefinitionHydrator;
    private readonly IPayloadSchemaBuilder _payloadSchemaBuilder;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrchestrationButterMorphSchemaImporter"/> class.
    /// </summary>
    /// <param name="jsonSchemaImporter">ButterMorph JSON Schema importer.</param>
    /// <param name="payloadSchemaDefinitionHydrator">ButterMorph payload schema definition hydrator.</param>
    /// <param name="payloadSchemaBuilder">ButterMorph payload schema builder.</param>
    public OrchestrationButterMorphSchemaImporter(
        IJsonSchemaImporter jsonSchemaImporter,
        IPayloadSchemaDefinitionHydrator payloadSchemaDefinitionHydrator,
        IPayloadSchemaBuilder payloadSchemaBuilder)
    {
        _jsonSchemaImporter = jsonSchemaImporter ?? throw new ArgumentNullException(nameof(jsonSchemaImporter));
        _payloadSchemaDefinitionHydrator = payloadSchemaDefinitionHydrator ?? throw new ArgumentNullException(nameof(payloadSchemaDefinitionHydrator));
        _payloadSchemaBuilder = payloadSchemaBuilder ?? throw new ArgumentNullException(nameof(payloadSchemaBuilder));
    }

    /// <inheritdoc />
    public bool TryImport(SchemaBinding binding, out IStructureSchema schema, out string message)
    {
        schema = null;
        message = string.Empty;

        if (binding?.Snapshot is null)
        {
            message = "Schema binding does not have a resolved snapshot.";
            return false;
        }

        var snapshot = binding.Snapshot;
        if (string.IsNullOrWhiteSpace(snapshot.SchemaJson))
        {
            message = $"Schema '{binding.ContractKey}' v{binding.ContractVersion} does not contain schema JSON.";
            return false;
        }

        try
        {
            var schemaJson = snapshot.SchemaJson;
            if (IsButterMorphSnapshot(snapshot) &&
                LooksLikePayloadSchemaDefinition(schemaJson) &&
                !TryBuildJsonSchemaFromButterMorphDefinition(binding, schemaJson, out schemaJson, out message))
            {
                return false;
            }

            var result = _jsonSchemaImporter.Import(new JsonSchemaImportRequest
            {
                Name = string.IsNullOrWhiteSpace(binding.ContractKey) ? snapshot.ContractKey : binding.ContractKey,
                Version = binding.ContractVersion.ToString(),
                JsonSchema = schemaJson
            });

            if (result.Succeeded && result.Schema is not null)
            {
                schema = result.Schema;
                return true;
            }

            message = BuildDiagnosticsMessage(binding, result.Diagnostics);
            return false;
        }
        catch (Exception exception)
        {
            message = $"Schema '{binding.ContractKey}' v{binding.ContractVersion} could not be imported by ButterMorph: {exception.Message}";
            return false;
        }
    }

    private bool TryBuildJsonSchemaFromButterMorphDefinition(
        SchemaBinding binding,
        string schemaJson,
        out string jsonSchema,
        out string message)
    {
        jsonSchema = string.Empty;
        message = string.Empty;

        PayloadSchemaDefinition definition;
        try
        {
            definition = JsonSerializer.Deserialize<PayloadSchemaDefinition>(schemaJson, SerializerOptions);
        }
        catch (JsonException exception)
        {
            message = $"Schema '{binding.ContractKey}' v{binding.ContractVersion} contains an invalid ButterMorph payload definition: {exception.Message}";
            return false;
        }

        if (definition is null)
        {
            message = $"Schema '{binding.ContractKey}' v{binding.ContractVersion} contains an empty ButterMorph payload definition.";
            return false;
        }

        var input = _payloadSchemaDefinitionHydrator.Hydrate(definition);
        if (string.IsNullOrWhiteSpace(input.Key))
        {
            input.Key = binding.ContractKey;
        }

        if (string.IsNullOrWhiteSpace(input.Version))
        {
            input.Version = binding.ContractVersion.ToString();
        }

        var result = _payloadSchemaBuilder.Build(
            input,
            Array.Empty<SchemaTypeCatalogItem>(),
            Array.Empty<FieldMetadataCatalogItem>());

        if (!result.Succeeded || string.IsNullOrWhiteSpace(result.JsonSchema))
        {
            message = BuildDiagnosticsMessage(binding, result.Diagnostics);
            return false;
        }

        jsonSchema = result.JsonSchema;
        return true;
    }

    private static bool IsButterMorphSnapshot(SchemaContractSnapshot snapshot)
        => string.Equals(snapshot.SchemaFormat, "ButterMorph", StringComparison.OrdinalIgnoreCase);

    private static bool LooksLikePayloadSchemaDefinition(string schemaJson)
    {
        if (string.IsNullOrWhiteSpace(schemaJson))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(schemaJson);
            return document.RootElement.ValueKind == JsonValueKind.Object &&
                document.RootElement.TryGetProperty("key", out _) &&
                document.RootElement.TryGetProperty("properties", out _);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string BuildDiagnosticsMessage(SchemaBinding binding, IEnumerable<object> diagnostics)
    {
        var diagnosticText = diagnostics is null
            ? string.Empty
            : string.Join("; ", diagnostics.Select(x => x?.ToString()).Where(x => !string.IsNullOrWhiteSpace(x)));

        return string.IsNullOrWhiteSpace(diagnosticText)
            ? $"Schema '{binding.ContractKey}' v{binding.ContractVersion} could not be imported by ButterMorph."
            : $"Schema '{binding.ContractKey}' v{binding.ContractVersion} could not be imported by ButterMorph: {diagnosticText}";
    }
}
