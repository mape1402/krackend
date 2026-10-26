using Krackend.Sagas.Orchestrations.Runtime.DependencyInjection;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Dispatching;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Recovery;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Hosting;
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
        /// <param name="configureRuntime">Runtime-owned Mule lane configuration.</param>
        /// <returns>Runtime builder.</returns>
        public static KrackendOrchestrationsRuntimeBuilder AddMule(
            this KrackendOrchestrationsRuntimeBuilder builder,
            Action<IMuleRegistrationBuilder> configure,
            Action<KrackendOrchestrationsMuleRuntimeOptions>? configureRuntime = null)
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

            builder.Services.AddOptions<SagaStatisticsMuleOptions>().BindConfiguration("Runtime:Statistics");
            builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, SagaStatisticsScheduler>());

            if (configureRuntime is not null)
            {
                builder.Services.Configure(configureRuntime);
            }

            builder.Services.Replace(ServiceDescriptor.Scoped<IIntakeBuffer, IntakeBufferMule>());
            builder.Services.Replace(ServiceDescriptor.Scoped<IRemoteCommandDispatcher, MuleRemoteCommandDispatcher>());
            builder.Services.Replace(ServiceDescriptor.Scoped<ITaskDispatchReplayService, MuleTaskDispatchReplayService>());
            builder.Services.Replace(ServiceDescriptor.Scoped<ITaskDispatchCancellationService>(provider =>
                new MuleTaskDispatchCancellationService(
                    provider.GetRequiredService<IMuleCancellationService>(),
                    () => provider.GetRequiredService<IMuleReplayService>(),
                    provider.GetRequiredService<IMuleStorage>())));
            builder.Services.TryAddSingleton<IMuleTerminalFailureMarker, DefaultMuleTerminalFailureMarker>();
            builder.Services.TryAddEnumerable(
                ServiceDescriptor.Singleton<IConfigureOptions<MuleSettings>, MuleRuntimeArtifactLifecycleOptionsConfigurer>());

            return builder;
        }
    }
}
