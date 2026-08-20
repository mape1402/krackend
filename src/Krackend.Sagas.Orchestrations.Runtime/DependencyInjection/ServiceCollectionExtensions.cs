using Krackend.Sagas.Orchestrations.Runtime.Ingress;
using Krackend.Sagas.Orchestrations.Runtime.Ingress.Http;
using Krackend.Sagas.Orchestrations.Runtime.Ingress.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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
            services.TryAddScoped<IMessagingAdapter, DefaultMessagingAdapter>();
            services.AddKeyedScoped<IIngressConector, MessagingIngressConnector>(IngressTransport.Messaging);
            services.AddKeyedScoped<IIngressConector, DefaultHttpIngressConnector>(IngressTransport.Http);
            services.AddHostedService<IngressRegistryBackgroundService>();

            return new KrackendOrchestrationsRuntimeBuilder(services);
        }
    }
}
