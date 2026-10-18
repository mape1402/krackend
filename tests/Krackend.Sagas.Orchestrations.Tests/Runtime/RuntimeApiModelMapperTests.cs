using System.Text.Json.Nodes;
using System.Reflection;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Runtime.Api;

namespace Krackend.Sagas.Orchestrations.Tests.Runtime;

public sealed class RuntimeApiModelMapperTests
{
    [Fact]
    public void ArtifactMapperNormalizesOptionalLifecycleValues()
    {
        var artifact = new RuntimeOrchestrationArtifact
        {
            Id = Id.New(),
            OrchestrationDefinitionKey = "sales.created",
            ArtifactType = "orchestration-version-snapshot",
            SourceOrchestrationVersionId = Id.New(),
            Version = new SemanticVersion(1, 0, 0),
            ArtifactChecksum = new Checksum("checksum"),
            ArtifactPayload = JsonNode.Parse("{}")!,
            Status = RuntimeOrchestrationArtifactStatus.Ready,
            LifecycleStatus = RuntimeOrchestrationLifecycleStatus.Active,
            IsActive = true,
            DeployedOnUtc = DateTime.UtcNow,
            LastReleaseId = null!,
            LastControlPlaneArtifactId = null!,
            ReleaseStrategy = null!,
            ProjectionError = null!
        };

        var mapper = typeof(RuntimeArtifactApiModel).Assembly
            .GetType("Krackend.Sagas.Orchestrations.Runtime.Api.RuntimeApiModelMapper")!;
        var result = (RuntimeArtifactApiModel)mapper
            .GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .Single(method => method.Name == "ToApiModel" && method.GetParameters()[0].ParameterType == typeof(RuntimeOrchestrationArtifact))
            .Invoke(null, [artifact])!;

        Assert.Equal(string.Empty, result.LastReleaseId);
        Assert.Equal(string.Empty, result.LastControlPlaneArtifactId);
        Assert.Equal(string.Empty, result.ReleaseStrategy);
        Assert.Equal(string.Empty, result.ProjectionError);
    }
}
