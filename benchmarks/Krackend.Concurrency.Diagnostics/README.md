# Concurrency diagnostic reproductions

## Current verification: Mule 2.4.0 and the actual runtime

`Regression/Regression.csproj` runs selected xUnit tests in process against the actual runtime/test projects. It avoids VSTest's testhost socket, which could not connect in this environment. The selected regression classes use controlled dependencies. `RuntimeParallelExecutionTests` and `RuntimeSustainedConcurrencyTests` additionally use transactional SQLite, Krackend repositories, and Mule's actual EF dispatcher/executors; only the external business service is controlled. The ingress for that laboratory is `IIntakeBuffer`, not a live broker.

```powershell
# From the repository root, after restoring the test and runner projects:
powershell -NoProfile -File benchmarks/Krackend.Concurrency.Diagnostics/verify-concurrency.ps1 -Sustained
```

Restore from the configured package feed in a fresh environment. The local verification used packages built from the verified Mule v2.4.0 tag because NuGet DNS was unavailable. Exact provenance, measurements, migration and provider limits are in [the implementation results](../../docs/concurrency-implementation-results-20261119.md). The script writes separate JSON results for the regression, parallel and sustained groups under `artifacts/`; `-SkipBuild` reuses an already built candidate. The runner supports selected parameterless Fact tests and InlineData theories, and does not replace the full solution/infrastructure suite.

The implementation now queues optional remote notifications separately from local SignalR delivery. Both buffers are bounded; publication never waits for either consumer. Remote events are not echoed. Queued metadata is snapshotted without retaining the runtime payload, and overflow or failed delivery requests reconciliation through the existing Redis `resync` wire message. Providers can override the new default `IRuntimeReactiveFanout.PublishResyncAsync` method.

The coordinator rejects success after failed/lost renewal or caller cancellation, including when the mutation delegate ignores cancellation. The relational runtime also fences writes against the active lease in the same short transaction. Publications occur outside the mutation lease, and ordinary contention defers the same Mule action without consuming its functional retry budget.

Mule 2.4.0 is now integrated. The sections below preserve the earlier causal reproductions and their original results; they are historical diagnostics, not the current candidate's acceptance suite. Their linked production sources have changed since those results were captured. Use `verify-concurrency.ps1` to validate the current candidate.

## Historical cross-instance pipeline reproductions

`Pipeline/Pipeline.csproj` adds two reproductions using the unmodified Mule 2.3.2 executor, bounded dispatch queue, commit notifier, settings, and retry policy, plus the coordinator linked from Krackend source. Its primitive types, storage, handler registry, and metrics are controlled doubles. Each dispatched action receives its own DI scope and scoped storage. It does not use the potentially stale test binaries or require Redis, SQL Server, Azure Service Bus, or Docker.

From the `Pipeline` directory:

```powershell
dotnet restore Pipeline.csproj --source . --ignore-failed-sources -p:NuGetAudit=false
dotnet run --project Pipeline.csproj --no-restore
```

The first case holds instance A's lease, sends two A contenders through a real Mule lane configured with two execution slots, then queues a message for independent instance B. B's lease is immediately available in a separate control scope, but its queued message waits about two seconds because the A contenders occupy both execution slots. Both A actions then receive the default retry delay of about 30 seconds. Three distinct action scopes are verified.

The second case fills a bounded immediate dispatch queue, calls the real commit notifier while holding an instance lease, and verifies that the call and lease remain blocked until the queue is drained.

`Pipeline/results.json` records the observed results. Two execution slots and queue capacity one are deliberately small valid settings to expose the mechanisms deterministically; they are not claimed to be the deployed configuration. These cases demonstrate cross-instance head-of-line blocking and producer/lease coupling, not the full 50-message, 38-minute production incident.

`Pipeline/results-single-host.json` records the expanded reproduction: one process/root service provider, separate action scopes, and two locally contending actions whose persisted exceptions say `another runtime node`. The same run delayed the healthy instance by 2,103 ms and assigned ordinary retries of about 30 seconds. That message is hard-coded in Krackend; it does not identify a second pod. The actual incident's lease owner still requires persisted lease and execution evidence.

## Historical coordinator and flush checks

This console harness reproduces control-flow defects without Docker, Redis, SQL Server, or the socket connection required by VSTest. It does not change runtime behavior and is deliberately outside the solution.

Run from this directory with .NET 10 installed:

```powershell
dotnet restore Audit.csproj --source . --ignore-failed-sources -p:NuGetAudit=false
dotnet run --project Audit.csproj --no-restore
```

Exit code zero means the controls and Krackend regression checks passed, while the remaining Mule defects were reproduced. It does **not** mean the full runtime concurrency incident has been fixed.

The project links the production coordinator, notification publisher, queue, and message classes directly from `src`. `Stubs.cs` supplies only the primitive ID and the three-method repository contract used by the coordinator. The lease store and slow fanout are controlled test doubles; these are not database performance measurements or complete orchestrations.

`FastLaneRedisFlushService.cs` is an unchanged source snapshot from [Mule v2.3.2](https://github.com/mape1402/mule/blob/v2.3.2/src/Mule.FastLane.Redis/FastLaneRedisFlushService.cs), the dependency pinned by Krackend main. `MuleStubs.cs` supplies the buffer and durable-store contracts needed to exercise its actual flush loop. Its fake durable store models an existing terminal action, a missing action, and committing only after a successful batch. No Redis server, Lua runtime, or real EF context participates in that reproduction.

Cases:

1. Control: twenty distinct instance IDs execute successfully.
2. Twenty contenders for **one already-held instance** exhaust the default two-second wait and amplify acquisition attempts.
3. Three optional notification publications no longer retain the instance lease; the next mutation is accepted even with a slow fanout provider configured.
4. Lost renewal is rejected instead of returning `true`, even when the delegate ignores cancellation.
5. A missing durable action poisons a terminal flush batch and blocks an unrelated healthy completion on each retry.

`results.json` preserves the original diagnostic run, before implementation. `results-after-krackend-fixes.json` records the updated checks. Timing and acquisition counts vary with scheduling. This harness does not measure Kubernetes restarts, production resource limits, Azure Service Bus behavior, or end-to-end orchestration latency.
