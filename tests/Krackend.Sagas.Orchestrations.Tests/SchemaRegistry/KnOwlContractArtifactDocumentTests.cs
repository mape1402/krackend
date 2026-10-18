using System.Reflection;
using KnOwl.Contracts.Artifacts;
using Krackend.Sagas.Orchestrations.SchemaRegistry.KnOwl.Catalog;

namespace Krackend.Sagas.Orchestrations.Tests.SchemaRegistry;

public sealed class KnOwlContractArtifactDocumentTests
{
    [Fact]
    public void ContractArtifactDocumentNormalizesNullableLegacyFields()
    {
        var type = CatalogAssembly.GetType("Krackend.Sagas.Orchestrations.SchemaRegistry.KnOwl.Catalog.KnOwlContractArtifactDocument")!;
        var document = Activator.CreateInstance(type)!;
        Set(document, "ContractKey", null);
        Set(document, "Topic", null);
        Set(document, "Name", null);
        Set(document, "VersionNumber", null);
        Set(document, "PayloadSchemaJson", null);
        Set(document, "ContentHash", null);
        Set(document, "SourceStatus", null);

        var artifact = (ContractArtifact)Invoke(document, "ToContractArtifact");

        Assert.Equal(string.Empty, artifact.Name);
        Assert.Equal(string.Empty, artifact.ContractKey);
        Assert.Equal(string.Empty, artifact.VersionNumber);
        Assert.Equal(string.Empty, artifact.PayloadSchemaJson);
        Assert.Equal(string.Empty, artifact.ContentHash);
        Assert.Equal(string.Empty, artifact.SourceStatus);
    }

    [Fact]
    public void ContractArtifactDocumentUsesTopicWhenContractKeyIsBlank()
    {
        var type = CatalogAssembly.GetType("Krackend.Sagas.Orchestrations.SchemaRegistry.KnOwl.Catalog.KnOwlContractArtifactDocument")!;
        var document = Activator.CreateInstance(type)!;
        Set(document, "ContractKey", " ");
        Set(document, "Topic", "events.sales.created");

        var artifact = (ContractArtifact)Invoke(document, "ToContractArtifact");

        Assert.Equal("events.sales.created", artifact.ContractKey);
    }

    [Fact]
    public void CommandContractArtifactsDocumentNormalizesMissingArtifacts()
    {
        var type = CatalogAssembly.GetType("Krackend.Sagas.Orchestrations.SchemaRegistry.KnOwl.Catalog.KnOwlCommandContractArtifactsDocument")!;
        var document = Activator.CreateInstance(type)!;
        Set(document, "CommandKey", null);
        Set(document, "Version", null);
        Set(document, "RequestArtifact", null);
        Set(document, "ReplyArtifact", null);

        var artifacts = (CommandContractArtifacts<ContractArtifact>)Invoke(document, "ToCommandContractArtifacts");

        Assert.Equal(string.Empty, artifacts.CommandKey);
        Assert.Equal(string.Empty, artifacts.Version);
        Assert.Null(artifacts.RequestArtifact);
        Assert.Null(artifacts.ReplyArtifact);
    }

    private static Assembly CatalogAssembly => typeof(KnOwlControlPlaneContractCatalogHttpClient).Assembly;

    private static object Invoke(object target, string methodName)
        => target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.Invoke(target, null)!;

    private static void Set(object target, string propertyName, object? value)
        => target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.SetValue(target, value);
}
