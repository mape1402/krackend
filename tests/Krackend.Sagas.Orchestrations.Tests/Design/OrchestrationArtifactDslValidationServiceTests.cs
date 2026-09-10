namespace Krackend.Sagas.Orchestrations.Tests.Design;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.TransformationConfigurations;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.ValidationConfigurations;
using Microsoft.Extensions.DependencyInjection;
using DesignApplicationServices = Krackend.Sagas.Orchestrations.ControlPlane.Application.Design.ServiceCollectionExtensions;

public sealed class OrchestrationArtifactDslValidationServiceTests
{
    [Fact]
    public void ValidateThrowsWhenEnabledTransformationHasNoDsl()
    {
        var service = CreateService();
        var version = CreateVersion();
        version.StageDefinitions[0].TaskDefinitions[0].HasTransformation = true;
        version.StageDefinitions[0].TaskDefinitions[0].Transformation = new TransformationDefinition
        {
            Engine = EngineType.DSL,
            Configuration = new DslTransformationConfiguration()
        };

        var exception = Assert.Throws<OrchestrationArtifactDslValidationException>(() => service.Validate(version));

        Assert.Equal("stage:fulfillment:task:reserve-inventory:transformation", exception.Path);
    }

    [Fact]
    public void ValidateThrowsWhenEnabledRequestValidationHasNoDsl()
    {
        var service = CreateService();
        var version = CreateVersion();
        var messaging = (MessagingTaskConfiguration)version.StageDefinitions[0].TaskDefinitions[0].Configuration;
        messaging.HasRequestValidation = true;
        messaging.RequestValidation = new ValidationDefinition
        {
            Engine = EngineType.DSL,
            Configuration = new DslValidationConfiguration()
        };

        var exception = Assert.Throws<OrchestrationArtifactDslValidationException>(() => service.Validate(version));

        Assert.Equal("stage:fulfillment:task:reserve-inventory:request-validation", exception.Path);
    }

    private static IOrchestrationArtifactDslValidationService CreateService()
    {
        var services = new ServiceCollection();
        DesignApplicationServices.AddOrchestratorDesignApplication(services);
        return services.BuildServiceProvider().GetRequiredService<IOrchestrationArtifactDslValidationService>();
    }

    private static OrchestrationVersion CreateVersion()
    {
        var versionId = Id.New();
        var stageId = Id.New();
        var taskId = Id.New();

        return new OrchestrationVersion
        {
            Id = versionId,
            OrchestrationDefinitionId = Id.New(),
            Version = new SemanticVersion(1, 0, 0),
            VersionLabel = "1.0.0",
            Checksum = new Checksum("checksum"),
            Status = OrchestrationVersionStatus.Approved,
            CreatedBy = "tests",
            CreatedOnUtc = DateTime.UtcNow,
            StageDefinitions =
            [
                new StageDefinition
                {
                    Id = stageId,
                    OrchestrationVersionId = versionId,
                    Key = "fulfillment",
                    Name = "Fulfillment",
                    Order = 1,
                    TaskDefinitions =
                    [
                        new TaskDefinition
                        {
                            Id = taskId,
                            StageDefinitionId = stageId,
                            Key = "reserve-inventory",
                            Name = "Reserve inventory",
                            Order = 1,
                            Kind = TaskKind.Messaging,
                            Configuration = new MessagingTaskConfiguration
                            {
                                Topic = "inventories.reserve",
                                Version = new SemanticVersion(1, 0, 0)
                            },
                            IsEnabled = true
                        }
                    ]
                }
            ]
        };
    }
}
