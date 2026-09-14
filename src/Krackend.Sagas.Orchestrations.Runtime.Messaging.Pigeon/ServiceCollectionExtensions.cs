using Krackend.Sagas.Orchestrations.Runtime.DependencyInjection;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Dispatching.Messaging;
using Krackend.Sagas.Orchestrations.Runtime.Ingress.Messaging;
using Krackend.Sagas.Orchestrations.Runtime.Messaging.Pigeon.Interceptors;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Krackend.Sagas.Orchestrations.Runtime.Messaging.Pigeon
{
    /// <summary>
    /// Provides dependency injection registration methods for the Pigeon runtime messaging adapter.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Registers Pigeon as the runtime messaging ingress and dispatch adapter.
        /// </summary>
        /// <param name="builder">Runtime builder to extend.</param>
        /// <param name="configuration">Configuration used by Pigeon.</param>
        /// <returns>The same runtime builder for chained configuration.</returns>
        public static KrackendOrchestrationsRuntimeBuilder AddPigeon(
            this KrackendOrchestrationsRuntimeBuilder builder,
            IConfiguration configuration)
            => builder.AddPigeon(configuration, _ => { });

        /// <summary>
        /// Registers Pigeon as the runtime messaging ingress and dispatch adapter.
        /// </summary>
        /// <param name="builder">Runtime builder to extend.</param>
        /// <param name="configuration">Configuration used by Pigeon.</param>
        /// <param name="configure">Additional Pigeon global settings configuration.</param>
        /// <returns>The same runtime builder for chained configuration.</returns>
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
