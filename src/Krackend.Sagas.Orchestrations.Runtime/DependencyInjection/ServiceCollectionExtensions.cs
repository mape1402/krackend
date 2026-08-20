using Krackend.Sagas.Orchestrations.Runtime.Buffering;
using Krackend.Sagas.Orchestrations.Runtime.Engine;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Artifacts;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Control;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Control.Decisions;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Control.Handlers;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Dispatching;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Dispatching.Messaging;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Promotion;
using Krackend.Sagas.Orchestrations.Runtime.Ingress;
using Krackend.Sagas.Orchestrations.Runtime.Ingress.Http;
using Krackend.Sagas.Orchestrations.Runtime.Ingress.Messaging;
using Krackend.Sagas.Orchestrations.Runtime.Metadata;
using Krackend.Sagas.Orchestrations.Runtime.Storage.InMemory;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Krackend.Sagas.Orchestrations.Runtime.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
        public static KrackendOrchestrationsRuntimeBuilder AddKrackendOrchestrationsRuntime(this IServiceCollection services)
        {
            services.TryAddSingleton<IIngressRegistry, IngressRegistry>();
            services.TryAddScoped<IGetAllIngressConfigurationsAccessor, DefaultIngressConfigurationAccessor>();
            services.TryAddScoped<IGetIngressConfigurationByArtifactAccessor, DefaultIngressConfigurationAccessor>();
            services.TryAddSingleton<RuntimeIngressBackchannelOptions>();
            services.TryAddScoped<IBackchannelTopicFormatter, DefaultBackchannelTopicFormatter>();
            services.TryAddScoped<IRuntimeIngressConfigurationKeyBuilder, DefaultRuntimeIngressConfigurationKeyBuilder>();
            services.TryAddScoped<IRuntimeIngressConfigurationProjector, DefaultRuntimeIngressConfigurationProjector>();
            services.TryAddScoped<IMessagingConfigurationSerializer, DefaultMessagingConfigurationSerializer>();
            services.TryAddScoped<IMessagingIngressAdapter, DefaultMessagingAdapter>();
            services.TryAddScoped<IIntakeBuffer, DefaultIntakeBuffer>();
            services.TryAddScoped<ISagaEngine, SagaEngine>();
            services.TryAddScoped<IPromoter, Promoter>();
            services.TryAddScoped<IDecisionControl, DecisionControl>();
            services.TryAddScoped<IDecisionExecutor, DecisionExecutor>();
            services.TryAddScoped<IDecisionHandler<StartStageDecision>, StartStageDecisionHandler>();
            services.TryAddScoped<IDecisionHandler<DispatchTaskDecision>, DispatchTaskDecisionHandler>();
            services.TryAddScoped<IDecisionHandler<CompleteStageDecision>, CompleteStageDecisionHandler>();
            services.TryAddScoped<IDecisionHandler<CompleteInstanceDecision>, CompleteInstanceDecisionHandler>();
            services.TryAddScoped<IDecisionHandler<CompleteCallbackDecision>, CompleteCallbackDecisionHandler>();
            services.TryAddScoped<IDecisionHandler<CompensateInstanceDecision>, CompensateInstanceDecisionHandler>();
            services.TryAddScoped<IRuntimeArtifactSerializer, DefaultRuntimeArtifactSerializer>();
            services.TryAddScoped<IRuntimeArtifactResolver, DefaultRuntimeArtifactResolver>();
            services.TryAddScoped<IResolvedOrchestrationArtifactAccessor, DefaultResolvedOrchestrationArtifactAccessor>();
            services.TryAddScoped<IRemoteCommandDispatcher, RemoteCommandDispatcher>();
            services.TryAddScoped<IMessagingCommandSerializer, DefaultMessagingCommandSerializer>();
            services.TryAddScoped<IMessagingDispatchAdapter, DefaultMessagingDispatchAdapter>();
            services.TryAddSingleton<InMemoryRuntimeStore>();
            services.TryAddScoped<IRuntimeStorageUnitOfWork, InMemoryRuntimeStorageUnitOfWork>();
            services.TryAddScoped<IRuntimeArtifactRepository, InMemoryRuntimeArtifactRepository>();
            services.TryAddScoped<IOrchestrationInstanceRepository, InMemoryOrchestrationInstanceRepository>();
            services.TryAddScoped<IStageExecutionRepository, InMemoryStageExecutionRepository>();
            services.TryAddScoped<ITaskExecutionRepository, InMemoryTaskExecutionRepository>();
            services.TryAddScoped<ITaskExecutionAttemptRepository, InMemoryTaskExecutionAttemptRepository>();
            services.TryAddScoped<ITaskDispatchRepository, InMemoryTaskDispatchRepository>();
            services.TryAddScoped<IExecutionTransitionRepository, InMemoryExecutionTransitionRepository>();
            services.TryAddScoped<ICompensationExecutionRepository, InMemoryCompensationExecutionRepository>();
            services.TryAddScoped<IRuntimeIngressConfigurationRepository, InMemoryRuntimeIngressConfigurationRepository>();
            services.TryAddScoped<DefaultInstanceMetadataAccessor>();
            services.TryAddScoped<IInstanceMetadataAccessor>(provider =>
                provider.GetRequiredService<DefaultInstanceMetadataAccessor>());
            services.TryAddScoped<IInstanceMetadataSetter>(provider =>
                provider.GetRequiredService<DefaultInstanceMetadataAccessor>());
            services.AddKeyedScoped<IIngressConector, MessagingIngressConnector>(IngressTransport.Messaging);
            services.AddKeyedScoped<IIngressConector, DefaultHttpIngressConnector>(IngressTransport.Http);
            services.AddKeyedScoped<IRemoteCommandExecutor, MessagingRemoteCommandExecutor>(RemoteCommandTransport.Messaging);
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, IngressRegistryBackgroundService>());

            return new KrackendOrchestrationsRuntimeBuilder(services);
        }
    }
}
