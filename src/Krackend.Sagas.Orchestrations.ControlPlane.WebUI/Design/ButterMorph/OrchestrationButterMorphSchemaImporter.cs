namespace Krackend.Sagas.Orchestrations.ControlPlane.WebUI.Design.ButterMorph;

using global::ButterMorph.Abstractions;
using global::ButterMorph.Json.Schema;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;

/// <summary>
/// Imports schema snapshots through ButterMorph JSON Schema compatibility services.
/// </summary>
public sealed class OrchestrationButterMorphSchemaImporter : IOrchestrationButterMorphSchemaImporter
{
    private readonly IJsonSchemaImporter _jsonSchemaImporter;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrchestrationButterMorphSchemaImporter"/> class.
    /// </summary>
    /// <param name="jsonSchemaImporter">ButterMorph JSON Schema importer.</param>
    public OrchestrationButterMorphSchemaImporter(IJsonSchemaImporter jsonSchemaImporter)
    {
        _jsonSchemaImporter = jsonSchemaImporter ?? throw new ArgumentNullException(nameof(jsonSchemaImporter));
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
            var result = _jsonSchemaImporter.Import(new JsonSchemaImportRequest
            {
                Name = string.IsNullOrWhiteSpace(binding.ContractKey) ? snapshot.ContractKey : binding.ContractKey,
                Version = binding.ContractVersion.ToString(),
                JsonSchema = snapshot.SchemaJson
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
