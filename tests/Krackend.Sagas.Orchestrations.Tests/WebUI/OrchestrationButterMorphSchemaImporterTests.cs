using ButterMorph.Json.Schema;
using ButterMorph.SchemaDesign;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.WebUI.Design.ButterMorph;
using Krackend.Sagas.Orchestrations.SchemaRegistry;
using NSubstitute;
using DesignSchemaContractSnapshot = Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.SchemaContractSnapshot;

namespace Krackend.Sagas.Orchestrations.Tests.WebUI;

public sealed class OrchestrationButterMorphSchemaImporterTests
{
    [Fact]
    public void ConstructorRejectsNullDependencies()
    {
        Assert.Throws<ArgumentNullException>(() => new OrchestrationButterMorphSchemaImporter(
            null!,
            new PayloadSchemaDefinitionHydrator(),
            new PayloadSchemaBuilder()));
        Assert.Throws<ArgumentNullException>(() => new OrchestrationButterMorphSchemaImporter(
            new JsonSchemaImporter(),
            null!,
            new PayloadSchemaBuilder()));
        Assert.Throws<ArgumentNullException>(() => new OrchestrationButterMorphSchemaImporter(
            new JsonSchemaImporter(),
            new PayloadSchemaDefinitionHydrator(),
            null!));
    }

    [Fact]
    public void TryImportConvertsButterMorphPayloadSnapshotIntoStructureSchema()
    {
        var importer = CreateImporter();
        var binding = CreateBinding(
            "commands.inventories.stock.discount",
            "ButterMorph",
            """
            {
              "key": "commands.inventories.stock.discount",
              "name": "Discount Stock",
              "description": "Discounts stock from inventory.",
              "version": "1.0.0",
              "type": "object",
              "properties": {
                "SaleId": {
                  "type": "string",
                  "required": true
                },
                "Quantity": {
                  "type": "integer",
                  "required": true
                }
              }
            }
            """);

        var imported = importer.TryImport(binding, out var schema, out var message);

        Assert.True(imported, message);
        Assert.NotNull(schema);
        Assert.Equal("commands.inventories.stock.discount", schema.Key);
        Assert.Equal("1.0.0", schema.Version);
        Assert.Contains(schema.Root.Children, child => child.Name == "SaleId");
        Assert.Contains(schema.Root.Children, child => child.Name == "Quantity");
    }

    [Fact]
    public void TryImportKeepsJsonSchemaSnapshotsOnJsonSchemaImporterPath()
    {
        var importer = CreateImporter();
        var binding = CreateBinding(
            "events.sales.sale.created",
            "JsonSchema",
            """
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "title": "Sale Created",
              "type": "object",
              "properties": {
                "SaleId": {
                  "type": "string"
                }
              },
              "required": [ "SaleId" ]
            }
            """);

        var imported = importer.TryImport(binding, out var schema, out var message);

        Assert.True(imported, message);
        Assert.NotNull(schema);
        Assert.Equal("events.sales.sale.created", schema.Key);
        Assert.Contains(schema.Root.Children, child => child.Name == "SaleId");
    }

    [Fact]
    public void TryImportRejectsMissingSnapshotsAndEmptySchemaJson()
    {
        var importer = CreateImporter();
        var missingSnapshot = CreateBinding("events.sales.missing", "JsonSchema", "{}");
        missingSnapshot.Snapshot = null;
        var emptySchema = CreateBinding("events.sales.empty", "JsonSchema", " ");

        var missingImported = importer.TryImport(missingSnapshot, out var missingSchema, out var missingMessage);
        var emptyImported = importer.TryImport(emptySchema, out var emptyResult, out var emptyMessage);
        var nullImported = importer.TryImport(null!, out var nullSchema, out var nullMessage);

        Assert.False(missingImported);
        Assert.Null(missingSchema);
        Assert.Contains("resolved snapshot", missingMessage, StringComparison.OrdinalIgnoreCase);
        Assert.False(emptyImported);
        Assert.Null(emptyResult);
        Assert.Contains("does not contain schema JSON", emptyMessage, StringComparison.OrdinalIgnoreCase);
        Assert.False(nullImported);
        Assert.Null(nullSchema);
        Assert.Contains("resolved snapshot", nullMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryImportReportsInvalidButterMorphPayloadDefinitions()
    {
        var importer = CreateImporter();
        var binding = CreateBinding(
            "commands.sales.invalid",
            "ButterMorph",
            """
            {
              "key": "commands.sales.invalid",
              "properties": "not-an-object"
            }
            """);

        var imported = importer.TryImport(binding, out var schema, out var message);

        Assert.False(imported);
        Assert.Null(schema);
        Assert.Contains("invalid ButterMorph payload definition", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryImportUsesBindingIdentityWhenHydratedButterMorphInputOmitsIt()
    {
        var capturedInput = default(PayloadSchemaDesignInput);
        var hydrator = Substitute.For<IPayloadSchemaDefinitionHydrator>();
        hydrator.Hydrate(Arg.Any<PayloadSchemaDefinition>()).Returns(new PayloadSchemaDesignInput
        {
            Key = " ",
            Version = string.Empty
        });
        var builder = Substitute.For<IPayloadSchemaBuilder>();
        builder
            .Build(
                Arg.Do<PayloadSchemaDesignInput>(input => capturedInput = input),
                Arg.Any<IReadOnlyCollection<SchemaTypeCatalogItem>>(),
                Arg.Any<IReadOnlyCollection<FieldMetadataCatalogItem>>())
            .Returns(new PayloadSchemaDesignResult
            {
                Succeeded = true,
                JsonSchema = """
                {
                  "$schema": "https://json-schema.org/draft/2020-12/schema",
                  "title": "Discount Stock",
                  "type": "object",
                  "properties": {
                    "SaleId": {
                      "type": "string"
                    }
                  }
                }
                """
            });
        var importer = new OrchestrationButterMorphSchemaImporter(
            new JsonSchemaImporter(),
            hydrator,
            builder);
        var binding = CreateBinding(
            "commands.sales.discount",
            "ButterMorph",
            """
            {
              "key": "commands.sales.discount",
              "properties": {
                "SaleId": {
                  "type": "string"
                }
              }
            }
            """);

        var imported = importer.TryImport(binding, out var schema, out var message);

        Assert.True(imported, message);
        Assert.NotNull(schema);
        Assert.NotNull(capturedInput);
        Assert.Equal("commands.sales.discount", capturedInput.Key);
        Assert.Equal("1.0.0", capturedInput.Version);
    }

    [Fact]
    public void TryImportReportsButterMorphPayloadBuilderFailures()
    {
        var builder = Substitute.For<IPayloadSchemaBuilder>();
        builder
            .Build(
                Arg.Any<PayloadSchemaDesignInput>(),
                Arg.Any<IReadOnlyCollection<SchemaTypeCatalogItem>>(),
                Arg.Any<IReadOnlyCollection<FieldMetadataCatalogItem>>())
            .Returns(new PayloadSchemaDesignResult
            {
                Succeeded = false,
                JsonSchema = string.Empty
            });
        var importer = new OrchestrationButterMorphSchemaImporter(
            new JsonSchemaImporter(),
            new PayloadSchemaDefinitionHydrator(),
            builder);
        var binding = CreateBinding(
            "commands.sales.builder.failure",
            "ButterMorph",
            """
            {
              "key": "commands.sales.builder.failure",
              "properties": {
                "SaleId": {
                  "type": "string"
                }
              }
            }
            """);

        var imported = importer.TryImport(binding, out var schema, out var message);

        Assert.False(imported);
        Assert.Null(schema);
        Assert.Contains("could not be imported by ButterMorph", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryImportReportsJsonSchemaDiagnostics()
    {
        var importer = CreateImporter();
        var binding = CreateBinding(
            "events.sales.invalid",
            "JsonSchema",
            "{");

        var imported = importer.TryImport(binding, out var schema, out var message);

        Assert.False(imported);
        Assert.Null(schema);
        Assert.Contains("could not be imported by ButterMorph", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryImportReportsJsonSchemaImporterExceptions()
    {
        var jsonImporter = Substitute.For<IJsonSchemaImporter>();
        jsonImporter
            .Import(Arg.Any<JsonSchemaImportRequest>())
            .Returns(_ => throw new InvalidOperationException("import exploded"));
        var importer = new OrchestrationButterMorphSchemaImporter(
            jsonImporter,
            new PayloadSchemaDefinitionHydrator(),
            new PayloadSchemaBuilder());
        var binding = CreateBinding(
            "events.sales.exploded",
            "JsonSchema",
            """
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "type": "object"
            }
            """);

        var imported = importer.TryImport(binding, out var schema, out var message);

        Assert.False(imported);
        Assert.Null(schema);
        Assert.Contains("import exploded", message, StringComparison.OrdinalIgnoreCase);
    }

    private static OrchestrationButterMorphSchemaImporter CreateImporter()
        => new(
            new JsonSchemaImporter(),
            new PayloadSchemaDefinitionHydrator(),
            new PayloadSchemaBuilder());

    private static SchemaBinding CreateBinding(string contractKey, string schemaFormat, string schemaJson)
        => new()
        {
            Id = Id.New(),
            ElementType = ElementType.Task,
            ElementId = Id.New(),
            ContractId = Id.New(),
            ContractKey = contractKey,
            ContractVersion = new SemanticVersion(1, 0, 0),
            RegistryProviderId = Id.New(),
            RegistryProviderKey = "knowl",
            ContractKind = SchemaContractKind.CommandRequest,
            Snapshot = new DesignSchemaContractSnapshot
            {
                ContractKind = SchemaContractKind.CommandRequest,
                RegistryProviderKey = "knowl",
                ContractId = $"knowl:{contractKey}",
                ContractKey = contractKey,
                ContractVersion = "1.0.0",
                SchemaFormat = schemaFormat,
                SchemaJson = schemaJson,
                ContentHash = "hash"
            }
        };
}
