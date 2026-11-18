using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Coordination;

internal sealed class DefaultOrchestrationInstanceCoordinator : IOrchestrationInstanceCoordinator
{
    private static readonly AsyncLocal<Dictionary<Id, DefaultOrchestrationInstanceCoordinator>> HeldInstances = new();

    private readonly IOrchestrationInstanceRepository _instanceRepository;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly OrchestrationCoordinationOptions _options;
    private readonly ILogger<DefaultOrchestrationInstanceCoordinator> _logger;

    public DefaultOrchestrationInstanceCoordinator(
        IOrchestrationInstanceRepository instanceRepository,
        IServiceScopeFactory scopeFactory,
        IOptions<OrchestrationCoordinationOptions> options,
        ILogger<DefaultOrchestrationInstanceCoordinator> logger)
    {
        _instanceRepository = instanceRepository ?? throw new ArgumentNullException(nameof(instanceRepository));
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _options = options?.Value ?? new OrchestrationCoordinationOptions();
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<bool> TryExecuteAsync(
        Id instanceId,
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default,
        bool waitForLease = true)
    {
        ArgumentNullException.ThrowIfNull(operation);

        if (!_options.Enabled || IsHeld(instanceId))
        {
            await operation(cancellationToken);
            return true;
        }

        var leaseId = $"runtime:{Id.New()}";
        var leaseDuration = ResolveLeaseDuration(_options);
        var acquisition = await TryAcquireLeaseAsync(instanceId, leaseId, leaseDuration, waitForLease, cancellationToken);
        if (acquisition.InstanceMissing)
        {
            await operation(cancellationToken);
            return true;
        }

        if (acquisition.Lease is null)
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

    private async Task<(bool InstanceMissing, OrchestrationInstanceLease Lease)> TryAcquireLeaseAsync(
        Id instanceId,
        string leaseId,
        TimeSpan leaseDuration,
        bool waitForLease,
        CancellationToken cancellationToken)
    {
        var waitWindow = ResolveLeaseUnavailableWait(_options);
        var retryDelay = ResolveLeaseUnavailableRetryDelay(_options);
        var deadline = waitForLease ? DateTime.UtcNow.Add(waitWindow) : DateTime.UtcNow;

        while (true)
        {
            var now = DateTime.UtcNow;
            try
            {
                var lease = await _instanceRepository.TryAcquireLease(
                    instanceId,
                    leaseId,
                    now,
                    now.Add(leaseDuration),
                    cancellationToken);
                if (lease is not null || now >= deadline)
                {
                    return (false, lease);
                }
            }
            catch (KeyNotFoundException)
            {
                return (true, null);
            }

            await Task.Delay(retryDelay, cancellationToken);
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
                await using var scope = _scopeFactory.CreateAsyncScope();
                var renewalRepository = scope.ServiceProvider.GetRequiredService<IOrchestrationInstanceRepository>();
                var renewed = await renewalRepository.TryAcquireLease(
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

    private bool IsHeld(Id instanceId)
        => HeldInstances.Value?.TryGetValue(instanceId, out var owner) == true && ReferenceEquals(owner, this);

    private void Push(Id instanceId)
    {
        var held = HeldInstances.Value is { } current
            ? new Dictionary<Id, DefaultOrchestrationInstanceCoordinator>(current)
            : new Dictionary<Id, DefaultOrchestrationInstanceCoordinator>();
        held[instanceId] = this;
        HeldInstances.Value = held;
    }

    private static void Pop(Id instanceId)
    {
        var held = HeldInstances.Value;
        if (held is null)
        {
            return;
        }

        var updated = new Dictionary<Id, DefaultOrchestrationInstanceCoordinator>(held);
        updated.Remove(instanceId);
        HeldInstances.Value = updated.Count == 0 ? null : updated;
    }

    private static TimeSpan ResolveLeaseDuration(OrchestrationCoordinationOptions options)
        => TimeSpan.FromSeconds(Math.Max(1, options.LeaseDurationSeconds));

    private static TimeSpan ResolveLeaseUnavailableWait(OrchestrationCoordinationOptions options)
        => TimeSpan.FromMilliseconds(Math.Max(0, options.LeaseUnavailableWaitMilliseconds));

    private static TimeSpan ResolveLeaseUnavailableRetryDelay(OrchestrationCoordinationOptions options)
        => TimeSpan.FromMilliseconds(Math.Max(1, options.LeaseUnavailableRetryDelayMilliseconds));
}
