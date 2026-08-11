using System.Reflection;

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
    public void AllMigratedProjectsAreReferencedByTestAssembly()
    {
        var referencedAssemblies = Assembly.GetExecutingAssembly()
            .GetReferencedAssemblies()
            .Select(assembly => assembly.Name)
            .Where(name => name is not null && name.StartsWith("Krackend.Sagas.Orchestrations", StringComparison.Ordinal))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        var expected = new[]
        {
            "Krackend.Sagas.Orchestrations",
            "Krackend.Sagas.Orchestrations.Abstractions",
            "Krackend.Sagas.Orchestrations.Client",
            "Krackend.Sagas.Orchestrations.Client.Abstractions",
            "Krackend.Sagas.Orchestrations.Client.Spider",
            "Krackend.Sagas.Orchestrations.Contracts",
            "Krackend.Sagas.Orchestrations.ControlPlane.Bootstrap",
            "Krackend.Sagas.Orchestrations.Design",
            "Krackend.Sagas.Orchestrations.Design.Interaction",
            "Krackend.Sagas.Orchestrations.Design.Storage.SqlServer",
            "Krackend.Sagas.Orchestrations.Design.WebUI",
            "Krackend.Sagas.Orchestrations.Distribution",
            "Krackend.Sagas.Orchestrations.Distribution.Interaction",
            "Krackend.Sagas.Orchestrations.Distribution.Storage.SqlServer",
            "Krackend.Sagas.Orchestrations.Distribution.WebUI",
            "Krackend.Sagas.Orchestrations.EntityFrameworkCore.SqlServer",
            "Krackend.Sagas.Orchestrations.Messaging.Abstractions",
            "Krackend.Sagas.Orchestrations.Messaging.Pigeon",
            "Krackend.Sagas.Orchestrations.Runtime.WebUI",
            "Krackend.Sagas.Orchestrations.Security",
            "Krackend.Sagas.Orchestrations.Security.Interaction",
            "Krackend.Sagas.Orchestrations.Security.Storage.SqlServer",
            "Krackend.Sagas.Orchestrations.Security.WebUI",
            "Krackend.Sagas.Orchestrations.Web",
            "Krackend.Sagas.Orchestrations.WebUI.Shell",
        };

        Assert.Equal(expected, referencedAssemblies);
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
