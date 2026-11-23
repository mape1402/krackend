using Microsoft.Extensions.Options;
using Mule;

namespace Krackend.Sagas.Orchestrations.Runtime.Buffering.Mule;

/// <summary>
/// Applies Krackend's capacity-budgeted Mule lane topology. Hosts get safe defaults without reproducing internal
/// priority and backpressure rules, while still retaining explicit advanced overrides.
/// </summary>
internal sealed class MuleRuntimeWorkLaneOptionsConfigurer(IOptions<KrackendOrchestrationsMuleRuntimeOptions> runtimeOptions)
    : IConfigureOptions<MuleSettings>
{
    public void Configure(MuleSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        // Runtime-owned work is activated directly after its durable save. Scheduled
        // recovery scans once at startup, wakes for due retries/timeouts, and is signaled
        // when an immediate activation is omitted. It preserves crash recovery without
        // continuously querying SQL while the realtime path is healthy.
        settings.ImmediateDispatch = true;
        settings.RecoveryMode = MuleRecoveryMode.Scheduled;

        var configured = runtimeOptions.Value;
        settings.DispatchInterval = ResolveInterval(configured.RecoveryPollingInterval, TimeSpan.FromSeconds(30));
        var defaults = KrackendMuleRuntimeCapacityProfile.Resolve(configured);
        EnsureLane(settings, MuleActionKeys.TriggerLane, 200, 8, defaults.Trigger, configured.Trigger, settings.DispatchInterval);
        // Callback recovery is latency-sensitive: a lost immediate wakeup must not
        // suspend a real-time saga until the sparse global disaster scan. Healthy
        // callbacks still run by direct activation; this interval is fallback only.
        EnsureLane(settings, MuleActionKeys.BackchannelLane, 400, 20, defaults.Backchannel, configured.Backchannel, TimeSpan.FromSeconds(1));
        EnsureLane(settings, MuleActionKeys.DispatchLane, 300, 14, defaults.Dispatch, configured.Dispatch, settings.DispatchInterval);
        EnsureLane(settings, MuleActionKeys.TimeoutLane, 350, 16, defaults.Timeout, configured.Timeout, TimeSpan.FromSeconds(1));
        EnsureLane(settings, MuleActionKeys.StatisticsLane, 10, 1, defaults.Statistics, configured.Statistics, settings.DispatchInterval);
    }

    private static void EnsureLane(
        MuleSettings settings,
        string lane,
        int priority,
        int weight,
        ResolvedMuleLaneOptions defaults,
        KrackendOrchestrationsMuleLaneOptions overrides,
        TimeSpan recoveryPollingInterval)
    {
        if (settings.Lanes.ContainsKey(lane)) return;

        settings.Lanes[lane] = new MuleLaneSettings
        {
            Priority = priority,
            Weight = weight,
            WorkerCount = ResolvePositive(overrides.WorkerCount, defaults.WorkerCount),
            MaxDegreeOfParallelism = ResolvePositive(overrides.MaxDegreeOfParallelism, defaults.MaxDegreeOfParallelism),
            PollingInterval = ResolveInterval(overrides.RecoveryPollingInterval, recoveryPollingInterval),
            DispatchBatchSize = ResolvePositive(overrides.DispatchBatchSize, defaults.DispatchBatchSize),
            DispatchQueueCapacity = ResolveNonNegative(overrides.DispatchQueueCapacity, defaults.DispatchQueueCapacity),
            ExecutionQueueCapacity = ResolveNonNegative(overrides.ExecutionQueueCapacity, defaults.ExecutionQueueCapacity),
            MaxDrainBatchesPerCycle = ResolvePositive(overrides.MaxDrainBatchesPerCycle, defaults.MaxDrainBatchesPerCycle),
            MaxDrainActionsPerCycle = ResolveNonNegative(overrides.MaxDrainActionsPerCycle, defaults.MaxDrainActionsPerCycle),
            DrainUntilEmpty = overrides.DrainUntilEmpty ?? defaults.DrainUntilEmpty
        };
    }

    private static int ResolvePositive(int? value, int fallback) => Math.Max(1, value ?? fallback);

    private static int ResolveNonNegative(int? value, int fallback) => Math.Max(0, value ?? fallback);

    private static TimeSpan ResolveInterval(TimeSpan? value, TimeSpan fallback)
        => value is { } interval && interval > TimeSpan.Zero ? interval : fallback;
}

internal static class KrackendMuleRuntimeCapacityProfile
{
    public static ResolvedMuleRuntimeCapacity Resolve(KrackendOrchestrationsMuleRuntimeOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var processors = Math.Max(1, Environment.ProcessorCount);
        var budget = options.Profile switch
        {
            KrackendMuleRuntimeProfile.Development => new CapacityBudget(7, Math.Clamp(processors * 2, 16, 48), 128),
            KrackendMuleRuntimeProfile.HighThroughput => new CapacityBudget(
                // Claim workers are asynchronous admission loops, not dedicated threads.
                // A tiny fixed pool makes ImmediateDispatch form a burst into waves before
                // the much larger executor can see the work. Scheduled recovery keeps
                // these readers asleep when there is no activation, so size admission for
                // realtime bursts instead of throttling it to the processor count.
                Math.Clamp(processors * 8, 128, 1_024),
                // Mule executors are asynchronous tasks, not dedicated threads. A
                // real-time profile must not force hundreds of independent sagas
                // through a few dozen artificial slots.
                Math.Clamp(processors * 128, 2_048, 8_192),
                4_096),
            KrackendMuleRuntimeProfile.Custom => new CapacityBudget(
                Math.Max(7, options.TotalWorkerCount ?? 7), Math.Max(13, options.TotalMaxDegreeOfParallelism ?? 32), 256),
            _ => new CapacityBudget(
                Math.Clamp(processors, 7, 24), Math.Clamp(processors * 4, 32, 192), 512)
        };

        budget = budget with
        {
            // Seven runtime lanes have durable work: trigger, callback, dispatch, timeout, statistics and two
            // artifact lifecycle lanes. Each needs one reader to remain independently recoverable.
            Workers = Math.Max(7, options.TotalWorkerCount ?? budget.Workers),
            Parallelism = Math.Max(13, options.TotalMaxDegreeOfParallelism ?? budget.Parallelism)
        };

        // Healthy callbacks use the direct dispatcher; Mule's backchannel lane is
        // recovery capacity. Give the hot trigger and network-dispatch paths the
        // majority of claim and execution slots.
        var coreWorkers = Allocate(budget.Workers - 3, 25, 15, 50, 10);
        // Reserve one execution slot for statistics and four for each of the two artifact lifecycle lanes.
        var coreParallelism = Allocate(budget.Parallelism - 9, 25, 15, 50, 10);

        return new ResolvedMuleRuntimeCapacity(
            CreateLane(budget, coreWorkers[0], coreParallelism[0]),
            CreateLane(budget, coreWorkers[1], coreParallelism[1]),
            CreateLane(budget, coreWorkers[2], coreParallelism[2]),
            CreateLane(budget, coreWorkers[3], coreParallelism[3]),
            new ResolvedMuleLaneOptions(1, 1, 16, 64, 1, 1, 16, false),
            CreateLane(budget, 1, 4));
    }

    private static ResolvedMuleLaneOptions CreateLane(CapacityBudget budget, int workers, int parallelism)
    {
        var queueCapacity = Math.Max(budget.QueueUnit, parallelism * 32);
        return new ResolvedMuleLaneOptions(
            workers,
            parallelism,
            Math.Min(256, Math.Max(16, parallelism * 2)),
            queueCapacity,
            queueCapacity,
            4,
            Math.Min(queueCapacity, parallelism * 8),
            false);
    }

    private static int[] Allocate(int total, params int[] weights)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(total, weights.Length);

        var allocation = Enumerable.Repeat(1, weights.Length).ToArray();
        var remaining = total - weights.Length;
        if (remaining == 0) return allocation;

        var weightTotal = weights.Sum();
        var fractions = new (int Index, decimal Fraction)[weights.Length];
        var allocated = 0;
        for (var index = 0; index < weights.Length; index++)
        {
            var exact = (decimal)remaining * weights[index] / weightTotal;
            var whole = (int)Math.Floor(exact);
            allocation[index] += whole;
            allocated += whole;
            fractions[index] = (index, exact - whole);
        }

        foreach (var remainder in fractions
                     .OrderByDescending(item => item.Fraction)
                     .ThenBy(item => item.Index)
                     .Take(remaining - allocated))
        {
            allocation[remainder.Index]++;
        }

        return allocation;
    }

    private readonly record struct CapacityBudget(int Workers, int Parallelism, int QueueUnit);
}

internal sealed record ResolvedMuleRuntimeCapacity(
    ResolvedMuleLaneOptions Trigger,
    ResolvedMuleLaneOptions Backchannel,
    ResolvedMuleLaneOptions Dispatch,
    ResolvedMuleLaneOptions Timeout,
    ResolvedMuleLaneOptions Statistics,
    ResolvedMuleLaneOptions Artifact);

internal sealed record ResolvedMuleLaneOptions(
    int WorkerCount,
    int MaxDegreeOfParallelism,
    int DispatchBatchSize,
    int DispatchQueueCapacity,
    int ExecutionQueueCapacity,
    int MaxDrainBatchesPerCycle,
    int MaxDrainActionsPerCycle,
    bool DrainUntilEmpty);
