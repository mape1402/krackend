using ButterMorph.Json.Schema;
using ButterMorph.SchemaDesign;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.WebUI.Design.ButterMorph;
using Krackend.Sagas.Orchestrations.SchemaRegistry;
using DesignSchemaContractSnapshot = Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.SchemaContractSnapshot;

namespace Krackend.Sagas.Orchestrations.Tests.WebUI;

public sealed class OrchestrationButterMorphSchemaImporterTests
{
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
