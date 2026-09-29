namespace Krackend.Sagas.Orchestrations.Tests.WebUI;

using Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;
using Krackend.Sagas.Orchestrations.ControlPlane.WebUI.Design.ButterMorph;

public sealed class OrchestrationButterMorphSourceMetadataFactoryTests
{
    [Fact]
    public void Create_WhenSourceIsTriggerMetadata_UsesHumanReadableDisplayName()
    {
        var factory = new OrchestrationButterMorphSourceMetadataFactory();
        var context = new OrchestrationSchemaContext
        {
            OrchestrationVersionId = "version-1",
            OrchestrationVersion = "1.0.0",
            StageKey = "stage",
            TaskKey = "task",
            Signature = "signature",
            Sources =
            [
                new OrchestrationSchemaSource
                {
                    Alias = "trigger_metadata",
                    SourceKind = OrchestrationSchemaContextSourceKind.TriggerMetadata
                }
            ]
        };

        var metadata = factory.Create(context);

        Assert.True(metadata.TryGetValue("trigger_metadata", out var sourceMetadata));
        Assert.Equal("Trigger Metadata", sourceMetadata.DisplayName);
    }
}
