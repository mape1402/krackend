using Krackend.Sagas.Orchestrations.Runtime.Buffering;
using Krackend.Sagas.Orchestrations.Runtime.Engine;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Control;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Dispatching;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Dispatching.Messaging;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Promotion;
using Krackend.Sagas.Orchestrations.Runtime.Ingress;
using Krackend.Sagas.Orchestrations.Runtime.Ingress.Http;
using Krackend.Sagas.Orchestrations.Runtime.Ingress.Messaging;
using Krackend.Sagas.Orchestrations.Runtime.Metadata;
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
            services.TryAddScoped<IMessagingConfigurationSerializer, DefaultMessagingConfigurationSerializer>();
            services.TryAddScoped<IMessagingIngressAdapter, DefaultMessagingAdapter>();
            services.TryAddScoped<IIntakeBuffer, DefaultIntakeBuffer>();
            services.TryAddScoped<ISagaEngine, SagaEngine>();
            services.TryAddScoped<IPromoter, Promoter>();
            services.TryAddScoped<IDecisionControl, DecisionControl>();
            services.TryAddScoped<IRemoteCommandDispatcher, RemoteCommandDispatcher>();
            services.TryAddScoped<IMessagingCommandSerializer, DefaultMessagingCommandSerializer>();
            services.TryAddScoped<IMessagingDispatchAdapter, DefaultMessagingDispatchAdapter>();
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
