using Krackend.Sagas.Orchestrations.EntityFrameworkCore.SqlServer.Infrastructure;
using Krackend.Sagas.Orchestrations.Engine.DurableWork;
using Mule.Configuration;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers Mule durable action storage on the Krackend runtime storage context.
/// </summary>
public static class RuntimeMuleStorageRegistrationExtensions
{
    /// <summary>
    /// Uses the Krackend runtime storage DbContext as Mule durable action storage and registers runtime actions.
    /// </summary>
    public static IMuleRegistrationBuilder UseKrackendSagasOrchestrationsRuntimeStorage(this IMuleRegistrationBuilder builder)
    {
        if (builder == null)
            throw new ArgumentNullException(nameof(builder));

        return builder
            .UseEntityFrameworkCore<RuntimeStorageDbContext>()
            .AddKrackendSagasOrchestrationsRuntimeActions();
    }
}
