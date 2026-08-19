using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime;
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
        var environment = scope.ServiceProvider.GetRequiredService<RuntimeEnvironmentDescriptor>();
        var artifacts = scope.ServiceProvider.GetRequiredService<IRuntimeArtifactRepository>();
        var synchronizer = scope.ServiceProvider.GetRequiredService<IRuntimeArtifactConsumerSynchronizer>();

        var activeArtifacts = (await artifacts.GetAll(environment.EnvironmentKey, cancellationToken))
            .Where(x => x.IsActive)
            .ToArray();

        foreach (var artifact in activeArtifacts)
            await synchronizer.Synchronize(artifact, cancellationToken);

        _logger.LogInformation(
            "Runtime synchronized {Count} active artifact consumer registrations for environment '{EnvironmentKey}'.",
            activeArtifacts.Length,
            environment.EnvironmentKey);
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
