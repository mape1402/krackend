using System.Reflection;
using ClientPigeonServices = Krackend.Sagas.Orchestrations.Client.Messaging.Pigeon.ServiceCollectionExtensions;
using ClientSpiderServices = Spider.Pipelines.Core.OrchestrationPipelineBuilderExtensions;
using KnOwlSchemaRegistryServices = Krackend.Sagas.Orchestrations.SchemaRegistry.KnOwl.DependencyInjection.ServiceCollectionExtensions;
using MuleRuntimeServices = Krackend.Sagas.Orchestrations.Runtime.Buffering.Mule.ServiceCollectionExtensions;
using RuntimeButterMorphServices = Krackend.Sagas.Orchestrations.Runtime.ButterMorph.DependencyInjection.ServiceCollectionExtensions;
using RuntimeGossipRedisServices = Krackend.Sagas.Orchestrations.Runtime.Gossip.Redis.ServiceCollectionExtensions;
using RuntimePigeonServices = Krackend.Sagas.Orchestrations.Runtime.Messaging.Pigeon.ServiceCollectionExtensions;
using SchemaRegistryServices = Krackend.Sagas.Orchestrations.SchemaRegistry.DependencyInjection.ServiceCollectionExtensions;

namespace Krackend.Sagas.Orchestrations.Tests.Architecture;

public sealed class PackageBoundaryTests
{
    [Fact]
    public void SourcePackagesDoNotContainEfMigrations()
    {
        var repositoryRoot = FindRepositoryRoot();
        var sourceRoot = Path.Combine(repositoryRoot, "src");

        var migrationDirectories = Directory.Exists(sourceRoot)
            ? Directory.GetDirectories(sourceRoot, "Migrations", SearchOption.AllDirectories)
            : [];

        Assert.Empty(migrationDirectories);
    }

    [Fact]
    public void SagasPackagesDoNotReferenceOldOrchestratorNamespaces()
    {
        var assemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(assembly => assembly.GetName().Name?.StartsWith("Krackend.Sagas.Orchestrations", StringComparison.Ordinal) == true)
            .ToArray();

        foreach (var assembly in assemblies)
        {
            Assert.DoesNotContain(
                assembly.GetTypes().Select(type => type.Namespace).Where(@namespace => @namespace is not null)!,
                @namespace => @namespace!.StartsWith("Orchestrator.", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void ActiveOrchestrationProjectsAreReferencedByTestAssembly()
    {
        var referencedAssemblies = Assembly.GetExecutingAssembly()
            .GetReferencedAssemblies()
            .Select(assembly => assembly.Name)
            .Append(typeof(ClientPigeonServices).Assembly.GetName().Name)
            .Append(typeof(ClientSpiderServices).Assembly.GetName().Name)
            .Append(typeof(KnOwlSchemaRegistryServices).Assembly.GetName().Name)
            .Append(typeof(MuleRuntimeServices).Assembly.GetName().Name)
            .Append(typeof(RuntimeButterMorphServices).Assembly.GetName().Name)
            .Append(typeof(RuntimeGossipRedisServices).Assembly.GetName().Name)
            .Append(typeof(RuntimePigeonServices).Assembly.GetName().Name)
            .Append(typeof(SchemaRegistryServices).Assembly.GetName().Name)
            .Where(name => name is not null && name.StartsWith("Krackend.Sagas.Orchestrations", StringComparison.Ordinal))
            .ToHashSet(StringComparer.Ordinal);

        var expected = new[]
        {
            "Krackend.Sagas.Orchestrations.Abstractions",
            "Krackend.Sagas.Orchestrations.Client",
            "Krackend.Sagas.Orchestrations.Client.Messaging.Pigeon",
            "Krackend.Sagas.Orchestrations.Client.Spider",
            "Krackend.Sagas.Orchestrations.Contracts",
            "Krackend.Sagas.Orchestrations.ControlPlane",
            "Krackend.Sagas.Orchestrations.ControlPlane.Application",
            "Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework",
            "Krackend.Sagas.Orchestrations.ControlPlane.WebUI",
            "Krackend.Sagas.Orchestrations.Runtime",
            "Krackend.Sagas.Orchestrations.Runtime.Buffering.Mule",
            "Krackend.Sagas.Orchestrations.Runtime.ButterMorph",
            "Krackend.Sagas.Orchestrations.Runtime.Gossip.Redis",
            "Krackend.Sagas.Orchestrations.Runtime.Messaging.Pigeon",
            "Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework",
            "Krackend.Sagas.Orchestrations.Runtime.WebUI",
            "Krackend.Sagas.Orchestrations.SchemaRegistry.Abstractions",
            "Krackend.Sagas.Orchestrations.SchemaRegistry.KnOwl",
            "Krackend.Sagas.Orchestrations.WebUI.Shell",
        };

        foreach (var assemblyName in expected)
        {
            Assert.Contains(assemblyName, referencedAssemblies);
        }

        Assert.DoesNotContain("Krackend.Sagas.Orchestrations.Design", referencedAssemblies);
        Assert.DoesNotContain("Krackend.Sagas.Orchestrations.Distribution", referencedAssemblies);
        Assert.DoesNotContain("Krackend.Sagas.Orchestrations.Security", referencedAssemblies);
        Assert.DoesNotContain("Krackend.Sagas.Orchestrations.Runtime.Storage.SqlServer", referencedAssemblies);
    }

    [Fact]
    public void CorePackagesDoNotReferencePigeonMessaging()
    {
        var repositoryRoot = FindRepositoryRoot();
        var restrictedProjectFiles = new[]
        {
            Path.Combine(repositoryRoot, "src", "Krackend.Sagas.Orchestrations.Abstractions", "Krackend.Sagas.Orchestrations.Abstractions.csproj"),
            Path.Combine(repositoryRoot, "src", "Krackend.Sagas.Orchestrations.Client", "Krackend.Sagas.Orchestrations.Client.csproj"),
            Path.Combine(repositoryRoot, "src", "Krackend.Sagas.Orchestrations.Contracts", "Krackend.Sagas.Orchestrations.Contracts.csproj"),
            Path.Combine(repositoryRoot, "src", "Krackend.Sagas.Orchestrations.Runtime", "Krackend.Sagas.Orchestrations.Runtime.csproj"),
        };

        foreach (var projectFile in restrictedProjectFiles)
        {
            var contents = File.ReadAllText(projectFile);

            Assert.DoesNotContain("Pigeon.Messaging", contents, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void RuntimeCoreDoesNotContainDemoBusinessHardcodes()
    {
        var repositoryRoot = FindRepositoryRoot();
        var runtimeRoot = Path.Combine(repositoryRoot, "src", "Krackend.Sagas.Orchestrations.Runtime");
        var forbiddenTerms = new[]
        {
            "events.sales",
            "sales.sale.created",
            "sale-fulfillment",
            "inventories.reserve",
            "payments.authorize",
        };

        foreach (var sourceFile in Directory.EnumerateFiles(runtimeRoot, "*.cs", SearchOption.AllDirectories))
        {
            var contents = File.ReadAllText(sourceFile);

            foreach (var forbiddenTerm in forbiddenTerms)
            {
                Assert.DoesNotContain(forbiddenTerm, contents, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "src")) &&
                File.Exists(Path.Combine(directory.FullName, "Krackend.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root could not be located.");
    }
}
