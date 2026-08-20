using Krackend.Sagas.Orchestrations.Runtime.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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

            return builder;
        }
    }
}
