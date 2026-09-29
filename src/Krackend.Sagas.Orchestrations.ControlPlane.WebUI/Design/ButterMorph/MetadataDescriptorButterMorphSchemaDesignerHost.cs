namespace Krackend.Sagas.Orchestrations.ControlPlane.WebUI.Design.ButterMorph;

using System.Text.RegularExpressions;
using System.Text.Json;
using global::ButterMorph.SchemaDesign;
using global::ButterMorph.Web.Razor;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;

/// <summary>
/// Provides ButterMorph payload schema designer integration for orchestration metadata descriptors.
/// </summary>
public sealed class MetadataDescriptorButterMorphSchemaDesignerHost : IButterMorphPayloadSchemaDesignerHost
{
    private const string ContextPrefix = "metadata:";
    private const string NewContextValue = "new";
    private const string DefaultVersion = "1.0.0";
    private const string SourceKeyMetadataKey = "sourceKey";
    private const string DefaultMetadataSchemaJson = """
        {
          "type": "object",
          "additionalProperties": true,
          "properties": {}
        }
        """;

    private static readonly Regex KeyPattern = new(
        "^[a-z][a-z0-9]*(?:_[a-z0-9]+)*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly IMetadataDescriptorRepository _repository;
    private readonly IMetadataDescriptorApplicationService _service;
    private readonly IPayloadSchemaDefinitionHydrator _payloadSchemaDefinitionHydrator;
    private readonly IPayloadSchemaBuilder _payloadSchemaBuilder;

    /// <summary>
    /// Initializes a new instance of the <see cref="MetadataDescriptorButterMorphSchemaDesignerHost"/> class.
    /// </summary>
    /// <param name="repository">Metadata descriptor repository dependency.</param>
    /// <param name="service">Metadata descriptor application service dependency.</param>
    /// <param name="payloadSchemaDefinitionHydrator">ButterMorph payload schema definition hydrator.</param>
    /// <param name="payloadSchemaBuilder">ButterMorph payload schema builder.</param>
    public MetadataDescriptorButterMorphSchemaDesignerHost(
        IMetadataDescriptorRepository repository,
        IMetadataDescriptorApplicationService service,
        IPayloadSchemaDefinitionHydrator payloadSchemaDefinitionHydrator,
        IPayloadSchemaBuilder payloadSchemaBuilder)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _payloadSchemaDefinitionHydrator = payloadSchemaDefinitionHydrator ?? throw new ArgumentNullException(nameof(payloadSchemaDefinitionHydrator));
        _payloadSchemaBuilder = payloadSchemaBuilder ?? throw new ArgumentNullException(nameof(payloadSchemaBuilder));
    }

    /// <inheritdoc />
    public async Task<ButterMorphPayloadSchemaDesignerLoadResult> Load(ButterMorphPayloadSchemaDesignerLoadRequest request)
    {
        if (!TryParseContext(request?.ContextKey, out var descriptorId, out var isNew))
        {
            return CreateLoadFailure("Metadata descriptor context is invalid.");
        }

        if (isNew)
        {
            return new ButterMorphPayloadSchemaDesignerLoadResult
            {
                Version = DefaultVersion,
                JsonSchema = DefaultMetadataSchemaJson,
                Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [SourceKeyMetadataKey] = string.Empty
                },
                MetadataDefinition = CreateMetadataDefinition(),
                SchemaTypes = CreateTemporalSchemaTypes(),
                ShowManualActions = false
            };
        }

        var descriptor = await TryGetDescriptor(descriptorId);
        if (descriptor is null)
        {
            return CreateLoadFailure("Metadata descriptor was not found.");
        }

        return new ButterMorphPayloadSchemaDesignerLoadResult
        {
            Key = descriptor.Key,
            Name = descriptor.DisplayName,
            Description = descriptor.Description ?? string.Empty,
            Version = DefaultVersion,
            Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [SourceKeyMetadataKey] = string.IsNullOrWhiteSpace(descriptor.SourceKey)
                    ? descriptor.Key
                    : descriptor.SourceKey
            },
            MetadataDefinition = CreateMetadataDefinition(),
            JsonSchema = string.IsNullOrWhiteSpace(descriptor.SchemaJson) ? DefaultMetadataSchemaJson : descriptor.SchemaJson,
            SchemaTypes = CreateTemporalSchemaTypes(),
            ShowManualActions = false
        };
    }

    /// <inheritdoc />
    public async Task<ButterMorphPayloadSchemaDesignerSaveResult> Save(ButterMorphPayloadSchemaDesignerSaveRequest request)
    {
        if (!TryParseContext(request?.ContextKey, out var descriptorId, out var isNew))
        {
            return CreateSaveFailure("Metadata descriptor context is invalid.");
        }

        if (request?.Definition is null)
        {
            return CreateSaveFailure("ButterMorph did not produce a metadata schema definition to save.");
        }

        var definition = request.Definition;
        var key = definition.Key?.Trim() ?? string.Empty;
        var hasSourceKeyMetadata = TryResolveSourceKey(definition, out var sourceKey);
        if (string.IsNullOrWhiteSpace(key))
        {
            return CreateSaveFailure("Metadata key is required.");
        }

        if (!KeyPattern.IsMatch(key))
        {
            return CreateSaveFailure("Metadata key must use lowercase segments separated only by underscores, starting with a letter.");
        }

        var jsonSchema = BuildJsonSchema(definition, out var schemaError);
        if (string.IsNullOrWhiteSpace(jsonSchema))
        {
            return CreateSaveFailure(schemaError);
        }

        var id = string.Empty;
        MetadataDescriptor descriptor = null;
        if (!isNew)
        {
            descriptor = await TryGetDescriptor(descriptorId);
            if (descriptor is null)
            {
                return CreateSaveFailure("Metadata descriptor was not found.");
            }

            id = descriptor.Id.ToString();
        }

        if (string.IsNullOrWhiteSpace(sourceKey))
        {
            sourceKey = !isNew && !hasSourceKeyMetadata
                ? string.IsNullOrWhiteSpace(descriptor.SourceKey) ? key : descriptor.SourceKey
                : key;
        }

        try
        {
            await _service.Upsert(new UpsertMetadataDescriptorCommand(
                id,
                key,
                sourceKey,
                string.IsNullOrWhiteSpace(definition.Name) ? key : definition.Name.Trim(),
                definition.Description?.Trim() ?? string.Empty,
                jsonSchema));
        }
        catch (InvalidOperationException exception)
        {
            return CreateSaveFailure(exception.Message);
        }

        return new ButterMorphPayloadSchemaDesignerSaveResult
        {
            Succeeded = true,
            Message = "Metadata schema saved."
        };
    }

    private string BuildJsonSchema(PayloadSchemaDefinition definition, out string message)
    {
        message = string.Empty;

        var input = _payloadSchemaDefinitionHydrator.Hydrate(definition);
        if (string.IsNullOrWhiteSpace(input.Key))
        {
            input.Key = definition.Key;
        }

        if (string.IsNullOrWhiteSpace(input.Name))
        {
            input.Name = string.IsNullOrWhiteSpace(definition.Name) ? definition.Key : definition.Name;
        }

        if (string.IsNullOrWhiteSpace(input.Version))
        {
            input.Version = DefaultVersion;
        }

        var result = _payloadSchemaBuilder.Build(
            input,
            CreateTemporalSchemaTypes(),
            Array.Empty<FieldMetadataCatalogItem>());

        if (result.Succeeded && !string.IsNullOrWhiteSpace(result.JsonSchema))
        {
            return result.JsonSchema;
        }

        message = BuildDiagnosticsMessage(result.Diagnostics);
        return string.Empty;
    }

    private static bool TryParseContext(string contextKey, out Id descriptorId, out bool isNew)
    {
        descriptorId = default;
        isNew = false;

        if (string.IsNullOrWhiteSpace(contextKey) ||
            !contextKey.StartsWith(ContextPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var value = contextKey[ContextPrefix.Length..];
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (string.Equals(value, NewContextValue, StringComparison.OrdinalIgnoreCase))
        {
            isNew = true;
            return true;
        }

        try
        {
            descriptorId = new Id(Ulid.Parse(value));
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private async Task<MetadataDescriptor> TryGetDescriptor(Id descriptorId)
    {
        try
        {
            return await _repository.GetById(descriptorId);
        }
        catch (KeyNotFoundException)
        {
            return null;
        }
    }

    private static ButterMorphPayloadSchemaDesignerLoadResult CreateLoadFailure(string message)
        => new()
        {
            Version = DefaultVersion,
            JsonSchema = DefaultMetadataSchemaJson,
            MetadataDefinition = CreateMetadataDefinition(),
            SchemaTypes = CreateTemporalSchemaTypes(),
            ShowManualActions = false,
            Message = message
        };

    private static SchemaMetadataDefinition CreateMetadataDefinition()
        => new()
        {
            Fields =
            [
                new SchemaMetadataFieldDefinition
                {
                    Key = SourceKeyMetadataKey,
                    Name = "Source key",
                    Description = "Exact metadata key expected on incoming messages. Matching uses exact lookup first, then case-insensitive fallback.",
                    DataType = SchemaMetadataDataType.String,
                    IsRequired = false
                }
            ]
        };

    private static bool TryResolveSourceKey(PayloadSchemaDefinition definition, out string sourceKey)
    {
        sourceKey = string.Empty;
        if (definition?.Metadata is not null &&
            definition.Metadata.TryGetValue(SourceKeyMetadataKey, out var value))
        {
            sourceKey = ReadMetadataValue(value)?.Trim() ?? string.Empty;
            return true;
        }

        return false;
    }

    private static string ReadMetadataValue(JsonElement value)
        => value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Null or JsonValueKind.Undefined => string.Empty,
            _ => value.ToString()
        };

    private static IReadOnlyCollection<SchemaTypeCatalogItem> CreateTemporalSchemaTypes()
        =>
        [
            CreateStringFormatType("datetime", "DateTime", "date-time", "Date and time value with timezone offset."),
            CreateStringFormatType("date", "Date", "date", "Calendar date value."),
            CreateStringFormatType("time", "Time", "time", "Clock time value."),
            CreateStringFormatType("timespan", "TimeSpan", "duration", "Elapsed duration value.")
        ];

    private static SchemaTypeCatalogItem CreateStringFormatType(
        string typeId,
        string name,
        string format,
        string description)
        => new()
        {
            TypeId = typeId,
            TypeVersionId = $"system.{typeId}.1_0_0",
            Name = name,
            VersionNumber = DefaultVersion,
            BaseType = "string",
            JsonSchema = $$"""
                {
                  "type": "string",
                  "format": "{{format}}",
                  "description": "{{description}}"
                }
                """,
            IsSystem = false
        };

    private static ButterMorphPayloadSchemaDesignerSaveResult CreateSaveFailure(string message)
        => new()
        {
            Succeeded = false,
            Message = string.IsNullOrWhiteSpace(message) ? "Metadata schema could not be saved." : message
        };

    private static string BuildDiagnosticsMessage(IEnumerable<object> diagnostics)
    {
        var diagnosticText = diagnostics is null
            ? string.Empty
            : string.Join("; ", diagnostics.Select(x => x?.ToString()).Where(x => !string.IsNullOrWhiteSpace(x)));

        return string.IsNullOrWhiteSpace(diagnosticText)
            ? "Metadata schema could not be built by ButterMorph."
            : $"Metadata schema could not be built by ButterMorph: {diagnosticText}";
    }
}
