using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Coordination;

internal sealed class DefaultOrchestrationInstanceCoordinator : IOrchestrationInstanceCoordinator
{
    private static readonly AsyncLocal<HashSet<Id>> HeldInstances = new();

    private readonly IOrchestrationInstanceRepository _instanceRepository;
    private readonly OrchestrationCoordinationOptions _options;
    private readonly ILogger<DefaultOrchestrationInstanceCoordinator> _logger;

    public DefaultOrchestrationInstanceCoordinator(
        IOrchestrationInstanceRepository instanceRepository,
        IOptions<OrchestrationCoordinationOptions> options,
        ILogger<DefaultOrchestrationInstanceCoordinator> logger)
    {
        _instanceRepository = instanceRepository ?? throw new ArgumentNullException(nameof(instanceRepository));
        _options = options?.Value ?? new OrchestrationCoordinationOptions();
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<bool> TryExecuteAsync(
        Id instanceId,
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        if (!_options.Enabled || IsHeld(instanceId))
        {
            await operation(cancellationToken);
            return true;
        }

        var leaseId = $"runtime:{Id.New()}";
        var leaseDuration = ResolveLeaseDuration(_options);
        var now = DateTime.UtcNow;
        OrchestrationInstanceLease lease;
        try
        {
            lease = await _instanceRepository.TryAcquireLease(
                instanceId,
                leaseId,
                now,
                now.Add(leaseDuration),
                cancellationToken);
        }
        catch (KeyNotFoundException)
        {
            await operation(cancellationToken);
            return true;
        }

        if (lease is null)
        {
            return false;
        }

        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var renewalTask = RenewLeaseUntilCancelledAsync(instanceId, leaseId, leaseDuration, linkedCancellation);
        Push(instanceId);

        try
        {
            await operation(linkedCancellation.Token);
            return true;
        }
        finally
        {
            Pop(instanceId);
            await linkedCancellation.CancelAsync();

            try
            {
                await renewalTask;
            }
            catch (OperationCanceledException)
            {
            }

            await _instanceRepository.ReleaseLease(instanceId, leaseId, CancellationToken.None);
        }
    }

    private async Task RenewLeaseUntilCancelledAsync(
        Id instanceId,
        string leaseId,
        TimeSpan leaseDuration,
        CancellationTokenSource linkedCancellation)
    {
        var renewalInterval = TimeSpan.FromMilliseconds(Math.Max(250, leaseDuration.TotalMilliseconds / 2));

        while (!linkedCancellation.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(renewalInterval, linkedCancellation.Token);
                var now = DateTime.UtcNow;
                var renewed = await _instanceRepository.TryAcquireLease(
                    instanceId,
                    leaseId,
                    now,
                    now.Add(leaseDuration),
                    linkedCancellation.Token);

                if (renewed is null)
                {
                    _logger.LogWarning(
                        "Lost orchestration lease '{leaseId}' for instance '{instanceId}'. Cancelling the active mutation scope.",
                        leaseId,
                        instanceId);
                    await linkedCancellation.CancelAsync();
                    return;
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    exception,
                    "Could not renew orchestration lease '{leaseId}' for instance '{instanceId}'. Cancelling the active mutation scope.",
                    leaseId,
                    instanceId);
                await linkedCancellation.CancelAsync();
                return;
            }
        }
    }

    private static bool IsHeld(Id instanceId)
        => HeldInstances.Value?.Contains(instanceId) == true;

    private static void Push(Id instanceId)
    {
        HeldInstances.Value ??= new HashSet<Id>();
        HeldInstances.Value.Add(instanceId);
    }

    private static void Pop(Id instanceId)
    {
        var held = HeldInstances.Value;
        if (held is null)
        {
            return;
        }

        held.Remove(instanceId);
        if (held.Count == 0)
        {
            HeldInstances.Value = null;
        }
    }

    private static TimeSpan ResolveLeaseDuration(OrchestrationCoordinationOptions options)
        => TimeSpan.FromSeconds(Math.Max(1, options.LeaseDurationSeconds));
}
