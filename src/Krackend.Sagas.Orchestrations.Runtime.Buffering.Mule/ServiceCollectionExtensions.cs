using Krackend.Sagas.Orchestrations.Runtime.DependencyInjection;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Dispatching;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Mule;
using Mule.Configuration;

namespace Krackend.Sagas.Orchestrations.Runtime.Buffering.Mule
{
    public static class ServiceCollectionExtensions
    {
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
            builder.Services.TryAddEnumerable(
                ServiceDescriptor.Singleton<IConfigureOptions<MuleSettings>, MuleRuntimeArtifactLifecycleOptionsConfigurer>());

            return builder;
        }
    }
}
