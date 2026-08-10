using Krackend.Sagas.Orchestrations.Contracts.Eventing;
using Krackend.Sagas.Orchestrations.Contracts.Events;
using Krackend.Sagas.Orchestrations.Design.Interaction;
using Krackend.Sagas.Orchestrations.Distribution.Interaction;
using Krackend.Sagas.Orchestrations.Security.Interaction;
using Microsoft.Extensions.DependencyInjection;
using DesignInteractionServices = Krackend.Sagas.Orchestrations.Design.Interaction.ServiceCollectionExtensions;
using DistributionInteractionServices = Krackend.Sagas.Orchestrations.Distribution.Interaction.ServiceCollectionExtensions;
using SecurityInteractionServices = Krackend.Sagas.Orchestrations.Security.Interaction.ServiceCollectionExtensions;

namespace Krackend.Sagas.Orchestrations.Tests.Interaction;

public sealed class ControlPlaneInteractionRegistrationTests
{
    [Fact]
    public void DesignInteractionRegistersCommandServicesMappersPoliciesAndEventHandlers()
    {
        var services = new ServiceCollection();

        DesignInteractionServices.AddOrchestratorDesignInteraction(services);

        AssertRegistered<IDomainInteractionService>(services);
        AssertRegistered<IOrchestrationInteractionService>(services);
        AssertRegistered<IOrchestrationVersionInteractionService>(services);
        AssertRegistered<IStageInteractionService>(services);
        AssertRegistered<ITaskInteractionService>(services);
        AssertRegistered<ITriggerBindingInteractionService>(services);
        AssertRegistered<IVariableInteractionService>(services);
        AssertRegistered<IParallelGroupInteractionService>(services);
        AssertRegistered<IBranchRuleInteractionService>(services);
        AssertRegistered<IOrchestrationVersionTransitionPolicy>(services);
        AssertRegistered<IIntegrationEventHandler<TeamCreatedEvent>>(services);
        AssertRegistered<IIntegrationEventHandler<TeamUpdatedEvent>>(services);
        AssertRegistered<IIntegrationEventHandler<TeamDisabledEvent>>(services);
    }

    [Fact]
    public void DistributionInteractionRegistersArtifactDeliveryAndLifecycleHandlers()
    {
        var services = new ServiceCollection();

        DistributionInteractionServices.AddOrchestratorDistributionInteraction(services);

        AssertRegistered<IRuntimeEnvironmentInteractionService>(services);
        AssertRegistered<IRuntimeNodeInteractionService>(services);
        AssertRegistered<IArtifactInteractionService>(services);
        AssertRegistered<IReleaseInteractionService>(services);
        AssertRegistered<IReleaseTargetInteractionService>(services);
        AssertRegistered<IArtifactDeliveryInteractionService>(services);
        AssertRegistered<IOrchestrationNodePolicyInteractionService>(services);
        AssertRegistered<IArtifactValidationPolicy>(services);
        AssertRegistered<IIntegrationEventHandler<OrchestrationVersionDeployedEvent>>(services);
        AssertRegistered<IIntegrationEventHandler<OrchestrationVersionDeprecatedEvent>>(services);
        AssertRegistered<IIntegrationEventHandler<OrchestrationVersionArchivedEvent>>(services);
        AssertRegistered<IIntegrationEventHandler<OrchestrationDefinitionCreatedEvent>>(services);
        AssertRegistered<IIntegrationEventHandler<OrchestrationDefinitionUpdatedEvent>>(services);
        AssertRegistered<IIntegrationEventHandler<OrchestrationDefinitionDeactivatedEvent>>(services);
    }

    [Fact]
    public void SecurityInteractionRegistersTeamServiceAndValidationPipeline()
    {
        var services = new ServiceCollection();

        SecurityInteractionServices.AddOrchestratorSecurityInteraction(services);

        AssertRegistered<ITeamInteractionService>(services);
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType.IsGenericType &&
            descriptor.ServiceType.GetGenericTypeDefinition().FullName == "Pelican.Mediator.IPipelineBehavior`2");
    }

    private static void AssertRegistered<TService>(IServiceCollection services)
    {
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(TService));
    }
}
