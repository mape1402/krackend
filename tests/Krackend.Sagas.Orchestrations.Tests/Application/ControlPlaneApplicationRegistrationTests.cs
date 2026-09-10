using Krackend.Sagas.Orchestrations.Contracts.Events;
using Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;
using Krackend.Sagas.Orchestrations.ControlPlane.Application.Distribution;
using Krackend.Sagas.Orchestrations.ControlPlane.Application.Security;
using Microsoft.Extensions.DependencyInjection;
using DesignApplicationServices = Krackend.Sagas.Orchestrations.ControlPlane.Application.Design.ServiceCollectionExtensions;
using DistributionApplicationServices = Krackend.Sagas.Orchestrations.ControlPlane.Application.Distribution.ServiceCollectionExtensions;
using SecurityApplicationServices = Krackend.Sagas.Orchestrations.ControlPlane.Application.Security.ServiceCollectionExtensions;

namespace Krackend.Sagas.Orchestrations.Tests.Application;

public sealed class ControlPlaneApplicationRegistrationTests
{
    [Fact]
    public void DesignApplicationRegistersCommandServicesMappersAndPolicies()
    {
        var services = new ServiceCollection();

        DesignApplicationServices.AddOrchestratorDesignApplication(services);

        AssertRegistered<IDomainApplicationService>(services);
        AssertRegistered<IOrchestrationApplicationService>(services);
        AssertRegistered<IOrchestrationVersionApplicationService>(services);
        AssertRegistered<IStageApplicationService>(services);
        AssertRegistered<ITaskApplicationService>(services);
        AssertRegistered<ITriggerBindingApplicationService>(services);
        AssertRegistered<IVariableApplicationService>(services);
        AssertRegistered<IParallelGroupApplicationService>(services);
        AssertRegistered<IBranchRuleApplicationService>(services);
        AssertRegistered<IOrchestrationVersionTransitionPolicy>(services);
        AssertRegistered<IOrchestrationArtifactDslValidationService>(services);
    }

    [Fact]
    public void DistributionApplicationRegistersArtifactDeliveryAndPublicationServices()
    {
        var services = new ServiceCollection();

        DistributionApplicationServices.AddOrchestratorDistributionApplication(services);

        AssertRegistered<IRuntimeNodeApplicationService>(services);
        AssertRegistered<IArtifactApplicationService>(services);
        AssertRegistered<IReleaseApplicationService>(services);
        AssertRegistered<IReleaseTargetApplicationService>(services);
        AssertRegistered<IArtifactDeliveryApplicationService>(services);
        AssertRegistered<IArtifactPublicationApplicationService>(services);
        AssertRegistered<IRuntimeNodeConnectionApplicationService>(services);
        AssertRegistered<IControlPlaneConnectionTokenIssuer>(services);
        AssertRegistered<IControlPlaneConnectionTokenValidator>(services);
        AssertRegistered<IRuntimeAccessTokenProvider>(services);
        AssertRegistered<IOrchestrationNodePolicyApplicationService>(services);
        AssertRegistered<IArtifactValidationPolicy>(services);
        AssertRegistered<IArtifactBuilder<OrchestrationVersionDeployedEvent>>(services);
        AssertRegistered<IArtifactBuilder<OrchestrationVersionDeprecatedEvent>>(services);
        AssertRegistered<IArtifactBuilder<OrchestrationVersionArchivedEvent>>(services);
    }

    [Fact]
    public void SecurityApplicationRegistersTeamServiceAndValidationPipeline()
    {
        var services = new ServiceCollection();

        SecurityApplicationServices.AddOrchestratorSecurityApplication(services);

        AssertRegistered<ITeamApplicationService>(services);
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType.IsGenericType &&
            descriptor.ServiceType.GetGenericTypeDefinition().FullName == "Pelican.Mediator.IPipelineBehavior`2");
    }

    private static void AssertRegistered<TService>(IServiceCollection services)
    {
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(TService));
    }
}
