using Krackend.Sagas.Orchestrations.Runtime.DependencyInjection;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Dispatching.Messaging;
using Krackend.Sagas.Orchestrations.Runtime.Ingress.Messaging;
using Krackend.Sagas.Orchestrations.Runtime.Messaging.Pigeon.Interceptors;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Krackend.Sagas.Orchestrations.Runtime.Messaging.Pigeon
{
    public static class ServiceCollectionExtensions
    {
        public static KrackendOrchestrationsRuntimeBuilder AddPigeon(
            this KrackendOrchestrationsRuntimeBuilder builder,
            IConfiguration configuration)
            => builder.AddPigeon(configuration, _ => { });

        public static KrackendOrchestrationsRuntimeBuilder AddPigeon(
            this KrackendOrchestrationsRuntimeBuilder builder,
            IConfiguration configuration,
            Action<GlobalSettingsBuilder> configure)
        {
            if (builder is null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            if (configuration is null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            if (configure is null)
            {
                throw new ArgumentNullException(nameof(configure));
            }

            builder.Services.AddPigeon(configuration, configure)
                .AddConsumeInterceptor<KrackendConsumeInterceptor>()
                .AddPublishInterceptor<KrackendPublishInterceptor>();
            builder.Services.TryAddSingleton<IPigeonIngressConsumerRegistry, PigeonIngressConsumerRegistry>();
            builder.Services.Replace(ServiceDescriptor.Scoped<IMessagingIngressAdapter, PigeonIngressAdapter>());
            builder.Services.Replace(ServiceDescriptor.Scoped<IMessagingDispatchAdapter, PigeonDispatchAdapter>());

            return builder;
        }
    }
}
