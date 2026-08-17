using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Krackend.Sagas.Orchestrations.Engine;

/// <summary>
/// Runs optional runtime storage warmups during host startup.
/// </summary>
public sealed class RuntimeStorageWarmupHostedService : IHostedService
{
    private const int MaxAttempts = 3;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RuntimeStorageWarmupHostedService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RuntimeStorageWarmupHostedService"/> class.
    /// </summary>
    public RuntimeStorageWarmupHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<RuntimeStorageWarmupHostedService> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var warmups = scope.ServiceProvider.GetServices<IRuntimeStorageWarmup>();
        foreach (var warmup in warmups)
            await RunWarmup(warmup, cancellationToken);
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task RunWarmup(IRuntimeStorageWarmup warmup, CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                await warmup.Warmup(cancellationToken);
                return;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception) when (attempt < MaxAttempts)
            {
                _logger.LogWarning(
                    exception,
                    "Runtime storage warmup {WarmupType} failed on attempt {Attempt}/{MaxAttempts}. Retrying.",
                    warmup.GetType().FullName,
                    attempt,
                    MaxAttempts);

                await Task.Delay(TimeSpan.FromMilliseconds(100 * attempt), cancellationToken);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    exception,
                    "Runtime storage warmup {WarmupType} failed after {MaxAttempts} attempts. Startup will continue without warmup.",
                    warmup.GetType().FullName,
                    MaxAttempts);
            }
        }
    }
}
