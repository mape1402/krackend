using Krackend.Sagas.Orchestrations.SchemaRegistry;
using Krackend.Sagas.Orchestrations.SchemaRegistry.KnOwl;
using Krackend.Sagas.Orchestrations.SchemaRegistry.KnOwl.Catalog;
using Krackend.Sagas.Orchestrations.SchemaRegistry.KnOwl.DependencyInjection;
using Krackend.Sagas.Orchestrations.SchemaRegistry.KnOwl.Resolution;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Krackend.Sagas.Orchestrations.Tests.SchemaRegistry;

public sealed class KnOwlSchemaRegistryRegistrationTests
{
    [Fact]
    public void AddKrackendKnOwlSchemaRegistryValidatesArguments()
    {
        IServiceCollection services = null!;
        Assert.Throws<ArgumentNullException>(() => services.AddKrackendKnOwlSchemaRegistry(_ => { }));

        services = new ServiceCollection();
        Assert.Throws<ArgumentNullException>(() => services.AddKrackendKnOwlSchemaRegistry(null!));
    }

    [Fact]
    public void AddKrackendKnOwlSchemaRegistryRegistersCatalogResolverAndOptions()
    {
        var services = new ServiceCollection();

        services.AddKrackendKnOwlSchemaRegistry(options =>
        {
            options.BaseUri = new Uri("https://knowl.local/api");
            options.Timeout = TimeSpan.Zero;
            options.ProviderKey = "knowl-control";
            options.SchemaFormat = "ButterMorph";
        });

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<KnOwlSchemaRegistryOptions>>().Value;
        var catalog = Assert.IsType<KnOwlControlPlaneContractCatalogHttpClient>(
            provider.GetRequiredService<IKnOwlControlPlaneContractCatalogClient>());
        var resolvers = provider.GetRequiredService<IEnumerable<ISchemaContractResolver>>().ToArray();
        var httpClient = GetHttpClient(catalog);

        Assert.Equal("knowl-control", options.ProviderKey);
        Assert.True(options.Enabled);
        Assert.Equal("ButterMorph", options.SchemaFormat);
        Assert.Equal(new Uri("https://knowl.local/api/"), httpClient.BaseAddress);
        Assert.Equal(TimeSpan.FromSeconds(30), httpClient.Timeout);
        Assert.Contains(resolvers, resolver => resolver is KnOwlSchemaContractResolver);
    }

    private static HttpClient GetHttpClient(KnOwlControlPlaneContractCatalogHttpClient catalog)
    {
        var field = typeof(KnOwlControlPlaneContractCatalogHttpClient)
            .GetField("_httpClient", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        return Assert.IsType<HttpClient>(field!.GetValue(catalog));
    }
}
