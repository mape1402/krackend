using Krackend.Sagas.Orchestrations.Runtime.DependencyInjection;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Dispatching;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Mule;
using Mule.Configuration;

namespace Krackend.Sagas.Orchestrations.Runtime.Buffering.Mule
{
    /// <summary>
    /// Registers Mule buffering and durable action services for Krackend orchestration runtime.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Adds Mule as the runtime intake buffer and remote command dispatch scheduler.
        /// </summary>
        /// <param name="builder">Runtime builder.</param>
        /// <param name="configure">Mule registration configuration.</param>
        /// <returns>Runtime builder.</returns>
        public static KrackendOrchestrationsRuntimeBuilder AddMule(
            this KrackendOrchestrationsRuntimeBuilder builder,
            Action<IMuleRegistrationBuilder> configure)
        {
            if (builder is null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            if (configure is null)
            {
                throw new ArgumentNullException(nameof(configure));
            }

            builder.Services.AddMule(mule =>
            {
                configure(mule);
                mule.AddActionsFromAssemblyContaining<TriggerAction>();
            });

            builder.Services.Replace(ServiceDescriptor.Scoped<IIntakeBuffer, IntakeBufferMule>());
            builder.Services.Replace(ServiceDescriptor.Scoped<IRemoteCommandDispatcher, MuleRemoteCommandDispatcher>());
            builder.Services.TryAddSingleton<IMuleTerminalFailureMarker, DefaultMuleTerminalFailureMarker>();
            builder.Services.TryAddEnumerable(
                ServiceDescriptor.Singleton<IConfigureOptions<MuleSettings>, MuleRuntimeArtifactLifecycleOptionsConfigurer>());

            return builder;
        }
    }
}
