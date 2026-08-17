using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Krackend.Sagas.Orchestrations.Engine;

/// <summary>
/// Runs optional runtime storage warmups during host startup.
/// </summary>
public sealed class RuntimeStorageWarmupHostedService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="RuntimeStorageWarmupHostedService"/> class.
    /// </summary>
    public RuntimeStorageWarmupHostedService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var warmups = scope.ServiceProvider.GetServices<IRuntimeStorageWarmup>();
        foreach (var warmup in warmups)
            await warmup.Warmup(cancellationToken);
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
