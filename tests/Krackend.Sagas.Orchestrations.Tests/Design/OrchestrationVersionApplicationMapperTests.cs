namespace Krackend.Sagas.Orchestrations.Tests.Design;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;

public sealed class OrchestrationVersionApplicationMapperTests
{
    [Fact]
    public void ToModelFormatsADraftWithoutASemanticVersion()
    {
        var version = new OrchestrationVersion
        {
            Id = Id.New(),
            OrchestrationDefinitionId = Id.New(),
            Version = null,
            Status = OrchestrationVersionStatus.Draft,
            VersionLabel = null!,
            Description = null!,
            Checksum = new Checksum("draft"),
            Notes = null!,
            TriggerBindings = null!,
            StageDefinitions = null!,
            VariableDefinitions = null!,
            CreatedOnUtc = DateTime.UtcNow,
            CreatedBy = "designer",
            ApprovedBy = null!,
            UpdatedBy = null!
        };

        var model = new OrchestrationVersionApplicationMapper().ToModel(version);

        Assert.Equal(string.Empty, model.Version);
        Assert.Equal(string.Empty, model.VersionLabel);
        Assert.Equal(string.Empty, model.Description);
        Assert.Equal(string.Empty, model.Notes);
        Assert.Equal(0, model.TriggerBindingsCount);
        Assert.Equal(0, model.StageDefinitionsCount);
        Assert.Equal(0, model.VariableDefinitionsCount);
    }
}
