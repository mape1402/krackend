using System.Reflection;
using MuleRuntimeServices = Krackend.Sagas.Orchestrations.Runtime.Buffering.Mule.ServiceCollectionExtensions;

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
            .Append(typeof(MuleRuntimeServices).Assembly.GetName().Name)
            .Where(name => name is not null && name.StartsWith("Krackend.Sagas.Orchestrations", StringComparison.Ordinal))
            .ToHashSet(StringComparer.Ordinal);

        var expected = new[]
        {
            "Krackend.Sagas.Orchestrations.Abstractions",
            "Krackend.Sagas.Orchestrations.Contracts",
            "Krackend.Sagas.Orchestrations.ControlPlane",
            "Krackend.Sagas.Orchestrations.ControlPlane.Application",
            "Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework",
            "Krackend.Sagas.Orchestrations.ControlPlane.WebUI",
            "Krackend.Sagas.Orchestrations.Runtime",
            "Krackend.Sagas.Orchestrations.Runtime.Buffering.Mule",
            "Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework",
            "Krackend.Sagas.Orchestrations.Runtime.WebUI",
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
