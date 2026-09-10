using Krackend.Sagas.Orchestrations.Abstractions.Distribution.Security;
using Krackend.Sagas.Orchestrations.Runtime.Buffering;
using Krackend.Sagas.Orchestrations.Runtime.Distribution;
using Krackend.Sagas.Orchestrations.Runtime.Engine;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Artifacts;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Control;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Control.Decisions;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Control.Handlers;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Dispatching;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Dispatching.Messaging;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Payloads;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Promotion;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Transformations;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Validation;
using Krackend.Sagas.Orchestrations.Runtime.Gossip;
using Krackend.Sagas.Orchestrations.Runtime.Ingress;
using Krackend.Sagas.Orchestrations.Runtime.Ingress.Http;
using Krackend.Sagas.Orchestrations.Runtime.Ingress.Messaging;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Reactive;
using Krackend.Sagas.Orchestrations.Runtime.Metadata;
using Krackend.Sagas.Orchestrations.Runtime.Replication;
using Krackend.Sagas.Orchestrations.Runtime.Storage.InMemory;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
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
            services.TryAddSingleton<IRuntimeIngressLocalState, RuntimeIngressLocalState>();
            services.TryAddScoped<IGetAllIngressConfigurationsAccessor, DefaultIngressConfigurationAccessor>();
            services.TryAddScoped<IGetIngressConfigurationByArtifactAccessor, DefaultIngressConfigurationAccessor>();
            services.TryAddSingleton<RuntimeIngressBackchannelOptions>();
            services.TryAddScoped<IBackchannelMessagingTopicFormatter, DefaultBackchannelMessagingTopicFormatter>();
            services.TryAddScoped<IRuntimeIngressConfigurationKeyBuilder, DefaultRuntimeIngressConfigurationKeyBuilder>();
            services.TryAddScoped<IRuntimeIngressConfigurationProjector, DefaultRuntimeIngressConfigurationProjector>();
            services.TryAddScoped<IMessagingConfigurationSerializer, DefaultMessagingConfigurationSerializer>();
            services.TryAddScoped<IMessagingIngressAdapter, DefaultMessagingAdapter>();
            services.TryAddScoped<IIntakeBuffer, DefaultIntakeBuffer>();
            services.TryAddScoped<ISagaEngine, SagaEngine>();
            services.TryAddScoped<IPromoter, Promoter>();
            services.TryAddScoped<IDecisionControl, DecisionControl>();
            services.TryAddScoped<IDecisionExecutor, DecisionExecutor>();
            services.TryAddScoped<IOrchestrationPayloadState, DefaultOrchestrationPayloadState>();
            services.TryAddScoped<IOrchestrationPayloadContextFactory, DefaultOrchestrationPayloadContextFactory>();
            services.TryAddScoped<IOrchestrationTransformationExecutor, DefaultOrchestrationTransformationExecutor>();
            services.TryAddScoped<IOrchestrationValidationExecutor, DefaultOrchestrationValidationExecutor>();
            services.TryAddScoped<ITaskDispatchRequestPayloadPreparer, DefaultTaskDispatchRequestPayloadPreparer>();
            services.TryAddScoped<IDecisionHandler<StartStageDecision>, StartStageDecisionHandler>();
            services.TryAddScoped<IDecisionHandler<DispatchTaskDecision>, DispatchTaskDecisionHandler>();
            services.TryAddScoped<IDecisionHandler<CompleteStageDecision>, CompleteStageDecisionHandler>();
            services.TryAddScoped<IDecisionHandler<CompleteInstanceDecision>, CompleteInstanceDecisionHandler>();
            services.TryAddScoped<IDecisionHandler<CompleteCallbackDecision>, CompleteCallbackDecisionHandler>();
            services.TryAddScoped<IDecisionHandler<CompensateInstanceDecision>, CompensateInstanceDecisionHandler>();
            services.TryAddScoped<IDecisionHandler<RetryDecision>, RetryDecisionHandler>();
            services.TryAddScoped<IRuntimeArtifactSerializer, DefaultRuntimeArtifactSerializer>();
            services.TryAddScoped<IRuntimeArtifactResolver, DefaultRuntimeArtifactResolver>();
            services.TryAddScoped<IResolvedOrchestrationArtifactAccessor, DefaultResolvedOrchestrationArtifactAccessor>();
            services.TryAddSingleton<IRuntimeReactiveEventPublisher, NoopRuntimeReactiveEventPublisher>();
            services.TryAddScoped<IRemoteCommandDispatcher, RemoteCommandDispatcher>();
            services.TryAddScoped<IMessagingCommandSerializer, DefaultMessagingCommandSerializer>();
            services.TryAddScoped<IMessagingDispatchAdapter, DefaultMessagingDispatchAdapter>();
            services.AddOptions<RuntimeReplicaOptions>().BindConfiguration("Runtime:Replica");
            services.AddOptions<RuntimeGossipOptions>().BindConfiguration("Runtime:Gossip");
            services.TryAddSingleton<IRuntimeReplicaIdentity, DefaultRuntimeReplicaIdentity>();
            services.TryAddScoped<IRuntimeArtifactProjectionScheduler, ImmediateRuntimeArtifactProjectionScheduler>();
            services.TryAddScoped<IRuntimeIngressStandupScheduler, ImmediateRuntimeIngressStandupScheduler>();
            services.TryAddScoped<IRuntimeArtifactReadyGossipHandler, RuntimeArtifactReadyGossipHandler>();
            services.TryAddScoped<IRuntimeArtifactReadyNotifier, RuntimeArtifactReadyNotifier>();
            services.TryAddSingleton<IRuntimeArtifactReadyGossipPublisher, NoopRuntimeArtifactReadyGossipPublisher>();
            services.TryAddSingleton<IDistributedCache, MemoryDistributedCache>();
            services.TryAddSingleton<IConnectionSecretHasher, Pbkdf2ConnectionSecretHasher>();
            services.TryAddSingleton<IConnectionSecretGenerator, SecureConnectionSecretGenerator>();
            services.TryAddSingleton<ITokenHashService, Sha256TokenHashService>();
            services.TryAddSingleton<IConnectionScopeFormatter, DefaultConnectionScopeFormatter>();
            services.TryAddSingleton<ConnectionTokenCacheKeyBuilder>();
            services.TryAddSingleton<ConnectionCredentialPackageSerializer>();
            services.AddDataProtection();
            services.TryAddSingleton<IRuntimeDesignNodeSecretProtector, DataProtectionRuntimeDesignNodeSecretProtector>();
            services.TryAddScoped<IControlPlaneDistributionSourceProvider, RuntimeDesignNodeDistributionSourceProvider>();
            services.TryAddScoped<IRuntimeArtifactDeploymentService, RuntimeArtifactDeploymentService>();
            services.TryAddScoped<IRuntimeArtifactDeliveryEndpointAuthenticator, RuntimeArtifactDeliveryEndpointAuthenticator>();
            services.TryAddScoped<IRuntimeConnectionTokenIssuer, RuntimeConnectionTokenIssuer>();
            services.TryAddScoped<IRuntimeConnectionTokenValidator, RuntimeConnectionTokenValidator>();
            services.AddHttpClient<IControlPlaneAccessTokenProvider, ControlPlaneAccessTokenProvider>();
            services.AddHttpClient<IRuntimeDesignNodeConnectionService, RuntimeDesignNodeConnectionService>();
            services.AddHttpClient<IControlPlaneArtifactPullService, ControlPlaneArtifactPullService>();
            services.TryAddSingleton<InMemoryRuntimeStore>();
            services.TryAddScoped<IRuntimeStorageUnitOfWork, InMemoryRuntimeStorageUnitOfWork>();
            services.TryAddScoped<IRuntimeDesignNodeRepository, InMemoryRuntimeDesignNodeRepository>();
            services.TryAddScoped<IRuntimeArtifactRepository, InMemoryRuntimeArtifactRepository>();
            services.TryAddScoped<IOrchestrationInstanceRepository, InMemoryOrchestrationInstanceRepository>();
            services.TryAddScoped<IStageExecutionRepository, InMemoryStageExecutionRepository>();
            services.TryAddScoped<ITaskExecutionRepository, InMemoryTaskExecutionRepository>();
            services.TryAddScoped<ITaskExecutionAttemptRepository, InMemoryTaskExecutionAttemptRepository>();
            services.TryAddScoped<ITaskDispatchRepository, InMemoryTaskDispatchRepository>();
            services.TryAddScoped<IExecutionTransitionRepository, InMemoryExecutionTransitionRepository>();
            services.TryAddScoped<ICompensationExecutionRepository, InMemoryCompensationExecutionRepository>();
            services.TryAddScoped<IRuntimeIngressConfigurationRepository, InMemoryRuntimeIngressConfigurationRepository>();
            services.TryAddScoped<DefaultOrchestrationMessageMetadataAccessor>();
            services.TryAddScoped<IOrchestrationMessageMetadataAccessor>(provider =>
                provider.GetRequiredService<DefaultOrchestrationMessageMetadataAccessor>());
            services.TryAddScoped<IOrchestrationMessageMetadataSetter>(provider =>
                provider.GetRequiredService<DefaultOrchestrationMessageMetadataAccessor>());
            services.TryAddScoped<DefaultOrchestrationExecutionResultMetadataAccessor>();
            services.TryAddScoped<IOrchestrationExecutionResultMetadataAccessor>(provider =>
                provider.GetRequiredService<DefaultOrchestrationExecutionResultMetadataAccessor>());
            services.TryAddScoped<IOrchestrationExecutionResultMetadataSetter>(provider =>
                provider.GetRequiredService<DefaultOrchestrationExecutionResultMetadataAccessor>());
            services.AddKeyedScoped<IIngressConector, MessagingIngressConnector>(IngressTransport.Messaging);
            services.AddKeyedScoped<IIngressConector, DefaultHttpIngressConnector>(IngressTransport.Http);
            services.AddKeyedScoped<IRemoteCommandExecutor, MessagingRemoteCommandExecutor>(RemoteCommandTransport.Messaging);
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, RuntimeReadyArtifactStartupService>());

            return new KrackendOrchestrationsRuntimeBuilder(services);
        }
    }
}
