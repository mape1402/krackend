using Krackend.Sagas.Orchestrations.Runtime.DependencyInjection;
using Krackend.Sagas.Orchestrations.Runtime.Ingress.Messaging;
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

            builder.Services.AddPigeon(configuration, configure);
            builder.Services.Replace(ServiceDescriptor.Scoped<IMessagingAdapter, PigeonMessagingAdapter>());

            return builder;
        }
    }
}
