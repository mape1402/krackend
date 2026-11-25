#nullable enable

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Infrastructure;

namespace Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework;

/// <summary>
/// Optional provider-owned runtime operations. Provider packages populate these
/// delegates while the portable Entity Framework adapter remains provider agnostic.
/// </summary>
public sealed class RuntimeEntityFrameworkProviderOptimizations
{
    /// <summary>Gets or sets an optional transaction-scoped instance lock operation.</summary>
    public Func<RuntimeDbContext, Id, string, CancellationToken, Task<bool?>>?
        TryAcquireTransactionLockAsync { get; set; }

    /// <summary>Gets or sets an optional compact callback graph reader.</summary>
    public Func<RuntimeDbContext, Id, CancellationToken, Task<TaskDispatchCallbackExecutionState?>>?
        TryReadCallbackGraphAsync { get; set; }

    /// <summary>Gets or sets an optional post-commit physical durability barrier.</summary>
    public Func<RuntimeDbContext, Task>? AwaitDurabilityAsync { get; set; }
}
