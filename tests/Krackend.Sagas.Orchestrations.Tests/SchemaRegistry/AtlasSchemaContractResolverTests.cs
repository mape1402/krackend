using Krackend.Sagas.Orchestrations.SchemaRegistry;
using Krackend.Sagas.Orchestrations.SchemaRegistry.Atlas;
using Krackend.Sagas.Orchestrations.SchemaRegistry.Atlas.Resolution;
using Microsoft.Extensions.Options;

namespace Krackend.Sagas.Orchestrations.Tests.SchemaRegistry;

public sealed class AtlasSchemaContractResolverTests
{
    [Fact]
    public async Task ResolveAsync_WhenAdapterIsDisabled_ReturnsNotConfigured()
    {
        var resolver = CreateResolver(options => options.Enabled = false);

        var result = await resolver.ResolveAsync(CreateRequest());

        Assert.Equal(SchemaContractResolutionStatus.NotConfigured, result.Status);
        Assert.Contains("disabled", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ResolveAsync_WhenEnabledWithoutBaseUri_ReturnsNotConfigured()
    {
        var resolver = CreateResolver(options => options.Enabled = true);

        var result = await resolver.ResolveAsync(CreateRequest());

        Assert.Equal(SchemaContractResolutionStatus.NotConfigured, result.Status);
        Assert.Contains("BaseUri", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ResolveAsync_WhenEndpointIsNotAvailableYet_ReturnsNotConfigured()
    {
        var resolver = CreateResolver(options =>
        {
            options.Enabled = true;
            options.BaseUri = new Uri("https://atlas.local");
        });

        var result = await resolver.ResolveAsync(CreateRequest());

        Assert.Equal(SchemaContractResolutionStatus.NotConfigured, result.Status);
        Assert.Contains("endpoint", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static AtlasSchemaContractResolver CreateResolver(Action<AtlasSchemaRegistryOptions> configure)
    {
        var options = new AtlasSchemaRegistryOptions();
        configure(options);

        return new AtlasSchemaContractResolver(Options.Create(options));
    }

    private static SchemaContractResolutionRequest CreateRequest()
        => new()
        {
            Reference = new SchemaContractReference
            {
                ProviderKey = "atlas",
                ContractKey = "sales.sale.created",
                ContractVersion = "1.0.0",
                Kind = SchemaContractKind.Event
            },
            RequireRemoteResolution = true
        };
}
