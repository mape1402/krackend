namespace Krackend.Sagas.Orchestrations.Tests.Design;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Artifacts;

public sealed class OrchestrationLifetimeArtifactTests
{
    [Fact]
    public void DeploymentArtifactPreservesVersionLifetimePolicy()
    {
        var definition = new OrchestrationDefinition { Id = Id.New(), Key = "orders", Name = "Orders", CreatedBy = "tests" };
        var version = new OrchestrationVersion { Id = Id.New(), OrchestrationDefinitionId = definition.Id,
            Version = new SemanticVersion(1, 0, 0), Checksum = new Checksum("hash"), CreatedBy = "tests",
            MaxInstanceDurationSeconds = 172800, IdleDurationSeconds = 3600 };

        var payload = new OrchestrationArtifactPayloadFactory().CreatePayloadJson(definition, version);
        var restored = new DefaultRuntimeArtifactSerializer().Deserialize(payload);

        Assert.Equal(172800, restored.MaxInstanceDurationSeconds);
        Assert.Equal(3600, restored.IdleDurationSeconds);
    }
}
