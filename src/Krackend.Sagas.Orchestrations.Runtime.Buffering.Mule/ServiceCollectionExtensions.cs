using Krackend.Sagas.Orchestrations.Runtime.DependencyInjection;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Dispatching;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Recovery;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Timeouts;
using Krackend.Sagas.Orchestrations.Runtime.Distribution;
using Krackend.Sagas.Orchestrations.Runtime.Ingress;
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
            // The runtime database and Mule may use different providers. Always route
            // deployment projection through the configured Mule client so its durable
            // action is written to the provider that will actually execute it.
            builder.Services.Replace(ServiceDescriptor.Scoped<IRuntimeArtifactProjectionScheduler,
                MuleRuntimeArtifactProjectionScheduler>());
            builder.Services.TryAddSingleton<MuleBackchannelImmediateDispatcher>();
            builder.Services.TryAddSingleton<IMuleBackchannelImmediateDispatcher>(provider =>
                provider.GetRequiredService<MuleBackchannelImmediateDispatcher>());
            builder.Services.AddSingleton<IHostedService>(provider =>
                provider.GetRequiredService<MuleBackchannelImmediateDispatcher>());
            builder.Services.TryAddSingleton<MuleCallbackRecoveryCursor>();
            builder.Services.TryAddScoped<IOrchestrationCallbackRecovery, MuleCallbackRecovery>();
            builder.Services.Replace(ServiceDescriptor.Scoped<IRemoteCommandDispatcher, MuleRemoteCommandDispatcher>());
            builder.Services.Replace(ServiceDescriptor.Scoped<IRuntimeIngressStandupScheduler, MuleRuntimeIngressStandupScheduler>());
            builder.Services.Replace(ServiceDescriptor.Scoped<ITaskResponseTimeoutScheduler, MuleTaskResponseTimeoutScheduler>());
            builder.Services.Replace(ServiceDescriptor.Scoped<IOrchestrationDurableWorkInspector, MuleOrchestrationDurableWorkInspector>());
            builder.Services.Replace(ServiceDescriptor.Scoped<ITaskDispatchReplayService, MuleTaskDispatchReplayService>());
            builder.Services.Replace(ServiceDescriptor.Scoped<ITaskDispatchCancellationService>(provider =>
                new MuleTaskDispatchCancellationService(
                    provider.GetRequiredService<IMuleCancellationService>(),
                    () => provider.GetRequiredService<IMuleReplayService>(),
                    provider.GetRequiredService<IMuleStorage>())));
            builder.Services.TryAddSingleton<IMuleTerminalFailureMarker, DefaultMuleTerminalFailureMarker>();
            builder.Services.TryAddEnumerable(
                ServiceDescriptor.Singleton<IConfigureOptions<MuleSettings>, MuleRuntimeArtifactLifecycleOptionsConfigurer>());
            builder.Services.TryAddEnumerable(
                ServiceDescriptor.Singleton<IConfigureOptions<MuleSettings>, MuleRuntimeWorkLaneOptionsConfigurer>());
            builder.Services.TryAddEnumerable(
                ServiceDescriptor.Singleton<IConfigureOptions<MuleSettings>, MuleRuntimeExecutionKeyOptionsConfigurer>());

            return builder;
        }

        /// <summary>
        /// Uses the response deadline persisted with each waiting task as the durable
        /// timeout source instead of creating and cancelling one Mule action per task.
        /// </summary>
        public static KrackendOrchestrationsRuntimeBuilder UseDeadlineScanningResponseTimeouts(
            this KrackendOrchestrationsRuntimeBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);
            builder.Services.Replace(
                ServiceDescriptor.Scoped<ITaskResponseTimeoutScheduler,
                    DeadlineScanningTaskResponseTimeoutScheduler>());
            return builder;
        }
    }
}
