using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Krackend.Sagas.Orchestrations.Web;

/// <summary>
/// Synchronizes persisted active runtime artifacts with message consumers during host startup.
/// </summary>
public sealed class RuntimeActiveArtifactConsumerHostedService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RuntimeActiveArtifactConsumerHostedService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RuntimeActiveArtifactConsumerHostedService"/> class.
    /// </summary>
    public RuntimeActiveArtifactConsumerHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<RuntimeActiveArtifactConsumerHostedService> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var synchronizer = scope.ServiceProvider.GetRequiredService<IRuntimeIngressSynchronizer>();

        _logger.LogInformation(
            "Runtime synchronizing active artifact ingress registrations.");

        await synchronizer.SynchronizeActiveArtifacts(cancellationToken);

        _logger.LogInformation(
            "Runtime synchronized active artifact ingress registrations.");
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
