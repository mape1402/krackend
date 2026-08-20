using TurtlePath.Spider.Transactions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Pelican.Mediator;
using Spider.Pipelines.Core;
using TurtlePath.Domain.Identifier;
using TurtlePath.EntityFrameworkCore;
using TurtlePath.ExceptionHandling;
using TurtlePath.ExceptionHandling.AspNetCore;
using TurtlePath.ExceptionHandling.Consumers;
using TurtlePath.Mapping;
using TurtlePath.Persistence;
using TurtlePath.Validation;

namespace Payments.Tests;

public sealed class TemplateCompositionTests
{
    [Fact]
    public void AddDefaults_registers_template_infrastructure()
    {
        var services = new ServiceCollection();
        var configuration = CreateConfiguration();
        var environment = new TestWebHostEnvironment();

        services.AddLogging();
        services.AddDefaults(configuration, environment);

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true
        });

        using var scope = provider.CreateScope();
        var scopedProvider = scope.ServiceProvider;

        Assert.NotNull(scopedProvider.GetRequiredService<IMediator>());
        Assert.NotNull(scopedProvider.GetRequiredService<ISpider>());
        Assert.NotNull(scopedProvider.GetRequiredService<ICIdFactory>());
        Assert.NotNull(scopedProvider.GetRequiredService<IDbContext>());
        Assert.NotNull(scopedProvider.GetRequiredService<IStorageReaderAdapter>());
        Assert.NotNull(scopedProvider.GetRequiredService<IStorageWriterAdapter>());
        Assert.NotNull(scopedProvider.GetRequiredService<IMapperAdapter>());
        Assert.NotNull(scopedProvider.GetRequiredService<IValidatorAdapter>());
        Assert.NotNull(scopedProvider.GetRequiredService<IExceptionHandler>());
        Assert.NotNull(scopedProvider.GetRequiredService<IHttpExceptionResponseFactory>());
        Assert.NotNull(scopedProvider.GetRequiredService<IHttpExceptionStatusCodeMapper>());
        Assert.NotNull(scopedProvider.GetRequiredService<IConsumerExceptionBoundary>());
        var transactionOptions = provider.GetRequiredService<IOptions<TransactionBoundaryOptions>>().Value;

        Assert.True(transactionOptions.Enabled);
        Assert.False(transactionOptions.IncludeQueries);
        Assert.Equal(30, transactionOptions.TimeoutSeconds);
        Assert.Contains(typeof(Payments.Business.Constants).Assembly, transactionOptions.RequestAssemblies);
        Assert.Contains(typeof(StartupExtensions).Assembly, transactionOptions.RequestAssemblies);
    }

    private static IConfiguration CreateConfiguration()
    {
        var values = new Dictionary<string, string>
        {
            ["ConnectionStrings:Default"] = "Server=(localdb)\\mssqllocaldb;Database=Payments_Composition;Trusted_Connection=True;MultipleActiveResultSets=true",
            ["Pigeon:Domain"] = "Payments",
            ["Pigeon:MessageBrokers:AzureServiceBus:ConnectionString"] = "",
            ["Pigeon:Outbox:Enabled"] = "true",
            ["TransactionBoundary:Enabled"] = "true",
            ["TransactionBoundary:IncludeQueries"] = "false",
            ["TransactionBoundary:IsolationLevel"] = "ReadCommitted",
            ["TransactionBoundary:TimeoutSeconds"] = "30"
        };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }

    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "Payments.Tests";

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public string EnvironmentName { get; set; } = Environments.Development;

        public string WebRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}
