namespace Krackend.Sagas.Orchestrations.Tests.Runtime;

using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Runtime.ButterMorph;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Payloads;

public sealed class ButterMorphSourceGraphBuilderTests
{
    [Theory]
    [InlineData("", "item")]
    [InlineData("   ", "item")]
    [InlineData("inventory-reservation", "inventory_reservation")]
    [InlineData("...///", "item")]
    public void AliasFormatterSanitizesKeysAndFallsBackToItem(string value, string expected)
    {
        var formatter = new ButterMorphAliasNameFormatter();

        var formatted = formatter.Format(value);

        Assert.Equal(expected, formatted);
    }

    [Fact]
    public void Build_WhenContextHasNoStagesOrVariables_ReturnsFallbackGraphsAndSkipsNullSources()
    {
        var builder = new ButterMorphSourceGraphBuilder(new ButterMorphAliasNameFormatter());
        var context = new OrchestrationPayloadContext
        {
            ContextPayload = JsonNode.Parse("{}")!,
            TriggerPayload = null!,
            StageKey = "inventory-reservation",
            TaskKey = "inventories.reserve"
        };

        var sources = builder.Build(context);

        Assert.Contains("context", sources.Keys);
        Assert.Contains("requests", sources.Keys);
        Assert.Contains("responses", sources.Keys);
        Assert.Contains("stages", sources.Keys);
        Assert.Contains("variables", sources.Keys);
        Assert.DoesNotContain("trigger", sources.Keys);
    }

    [Fact]
    public void Build_WhenContextContainsTasks_ProjectsRequestResponseAndSanitizedStageGraphs()
    {
        var builder = new ButterMorphSourceGraphBuilder(new ButterMorphAliasNameFormatter());
        var context = new OrchestrationPayloadContext
        {
            ContextPayload = JsonNode.Parse(
                """
                {
                  "stages": {
                    "inventory-reservation": {
                      "tasks": {
                        "inventories.reserve": {
                          "request": { "sku": "sku-1" },
                          "response": { "reserved": true }
                        },
                        "inventories.audit": {}
                      }
                    },
                    "payment": {}
                  },
                  "variables": { "tenant": "demo" }
                }
                """)!,
            TriggerPayload = JsonNode.Parse("""{"saleId":"sale-1"}""")!,
            StageKey = "inventory-reservation",
            TaskKey = "inventories.reserve"
        };

        var sources = builder.Build(context);

        Assert.Contains("trigger", sources.Keys);
        Assert.Contains("requests", sources.Keys);
        Assert.Contains("responses", sources.Keys);
        Assert.Contains("stages", sources.Keys);
        Assert.Contains("variables", sources.Keys);
    }
}
