using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Krackend.Sagas.Orchestrations.Runtime.Operations;

/// <summary>
/// Default runtime operational state controller.
/// </summary>
internal sealed class DefaultRuntimeOperationalStateController : IRuntimeOperationalStateController
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DefaultRuntimeOperationalStateController> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private RuntimeOperationalSnapshot _current = new(
        RuntimeOperationalState.Healthy,
        DateTime.UtcNow,
        Array.Empty<RuntimeDependencyProbeResult>(),
        "Runtime has not evaluated dependencies yet.");

    public DefaultRuntimeOperationalStateController(
        IServiceScopeFactory scopeFactory,
        ILogger<DefaultRuntimeOperationalStateController> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public RuntimeOperationalSnapshot Current => Volatile.Read(ref _current);

    public async Task<RuntimeOperationalSnapshot> EvaluateAsync(CancellationToken cancellationToken = default)
    {
        var observedOnUtc = DateTime.UtcNow;
        var results = new List<RuntimeDependencyProbeResult>();

        await using var scope = _scopeFactory.CreateAsyncScope();
        var probes = scope.ServiceProvider.GetServices<IRuntimeDependencyProbe>();
        foreach (var probe in probes)
        {
            try
            {
                results.Add(await probe.CheckAsync(cancellationToken));
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    exception,
                    "Runtime dependency probe '{DependencyName}' failed.",
                    probe.Name);
                results.Add(new RuntimeDependencyProbeResult(
                    probe.Name,
                    probe.Kind,
                    false,
                    exception.Message));
            }
        }

        var hasCriticalFailure = results.Any(static result =>
            result.Kind == RuntimeDependencyKind.Critical && !result.IsAvailable);
        var hasOptionalFailure = results.Any(static result =>
            result.Kind == RuntimeDependencyKind.Optional && !result.IsAvailable);

        var state = hasCriticalFailure
            ? RuntimeOperationalState.Closed
            : hasOptionalFailure ? RuntimeOperationalState.Degraded : RuntimeOperationalState.Healthy;
        var reason = hasCriticalFailure
            ? "A critical runtime dependency is unavailable."
            : hasOptionalFailure ? "One or more optional runtime dependencies are unavailable." : null;

        return await TransitionAsync(new RuntimeOperationalSnapshot(state, observedOnUtc, results, reason), cancellationToken);
    }

    public Task<RuntimeOperationalSnapshot> MarkRecoveringAsync(string reason, CancellationToken cancellationToken = default)
        => TransitionAsync(new RuntimeOperationalSnapshot(
            RuntimeOperationalState.Recovering,
            DateTime.UtcNow,
            Current.Dependencies,
            string.IsNullOrWhiteSpace(reason) ? "Runtime recovery is running." : reason),
            cancellationToken);

    private async Task<RuntimeOperationalSnapshot> TransitionAsync(
        RuntimeOperationalSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        RuntimeOperationalSnapshot previous;
        await _gate.WaitAsync(cancellationToken);
        try
        {
            previous = _current;
            if (previous.State == snapshot.State &&
                string.Equals(previous.Reason, snapshot.Reason, StringComparison.Ordinal) &&
                SameDependencies(previous.Dependencies, snapshot.Dependencies))
            {
                return previous;
            }

            Volatile.Write(ref _current, snapshot);
        }
        finally
        {
            _gate.Release();
        }

        await NotifyHandlersAsync(previous, snapshot, cancellationToken);
        return snapshot;
    }

    private async Task NotifyHandlersAsync(
        RuntimeOperationalSnapshot previous,
        RuntimeOperationalSnapshot current,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var handlers = scope.ServiceProvider.GetServices<IRuntimeDegradationHandler>();
            var context = new RuntimeOperationalStateChangedContext(previous, current);
            foreach (var handler in handlers)
            {
                try
                {
                    await handler.OnRuntimeStateChangedAsync(context, cancellationToken);
                }
                catch (Exception exception)
                {
                    _logger.LogError(
                        exception,
                        "Runtime degradation handler '{HandlerType}' failed while handling state '{State}'.",
                        handler.GetType().FullName,
                        current.State);
                }
            }
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Runtime operational state handlers could not be resolved.");
        }
    }

    private static bool SameDependencies(
        IReadOnlyCollection<RuntimeDependencyProbeResult> left,
        IReadOnlyCollection<RuntimeDependencyProbeResult> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        var leftOrdered = left.OrderBy(static item => item.Name, StringComparer.Ordinal).ToArray();
        var rightOrdered = right.OrderBy(static item => item.Name, StringComparer.Ordinal).ToArray();
        for (var index = 0; index < leftOrdered.Length; index++)
        {
            if (!Equals(leftOrdered[index], rightOrdered[index]))
            {
                return false;
            }
        }

        return true;
    }
}
