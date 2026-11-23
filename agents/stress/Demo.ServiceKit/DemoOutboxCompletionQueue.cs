using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Mule;

namespace Krackend.Demo;

/// <summary>
/// Coalesces successful direct-publication receipts. Publication is already durable
/// through the locked intent, so bookkeeping never delays the business response.
/// </summary>
public sealed class DemoOutboxCompletionQueue(
    IServiceScopeFactory scopes,
    ILogger<DemoOutboxCompletionQueue> logger) : BackgroundService
{
    private const int BatchSize = 1_000;
    private readonly Channel<global::Mule.Ulid> _published = Channel.CreateUnbounded<global::Mule.Ulid>(
        new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false
        });

    public void MarkPublished(global::Mule.Ulid actionId)
    {
        if (!_published.Writer.TryWrite(actionId))
            throw new InvalidOperationException($"Could not record published outbox action '{actionId}'.");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var batch = new List<global::Mule.Ulid>(BatchSize);
        try
        {
            while (await _published.Reader.WaitToReadAsync(stoppingToken))
            {
                // Give concurrent publishers one brief window to coalesce. This is not
                // on the publication or orchestration path.
                await Task.Delay(TimeSpan.FromMilliseconds(250), stoppingToken);
                batch.Clear();
                while (batch.Count < BatchSize && _published.Reader.TryRead(out var id))
                    batch.Add(id);
                if (batch.Count > 0) await CompleteBatchAsync(batch, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private async Task CompleteBatchAsync(IReadOnlyCollection<global::Mule.Ulid> ids, CancellationToken ct)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<DemoDbContext>();
            var now = DateTimeOffset.UtcNow;
            var token = global::Mule.Ulid.NewUlid();
            await db.Set<DurableAction>()
                .Where(action => ids.Contains(action.Id) && action.Status == DurableActionStatus.Locked)
                .ExecuteUpdateAsync(update => update
                    .SetProperty(action => action.Status, DurableActionStatus.Completed)
                    .SetProperty(action => action.LockedOnUtc, (DateTimeOffset?)null)
                    .SetProperty(action => action.NextAttemptOnUtc, (DateTimeOffset?)null)
                    .SetProperty(action => action.CompletedOnUtc, now)
                    .SetProperty(action => action.TerminalOnUtc, now)
                    .SetProperty(action => action.LastError, (string)null!)
                    .SetProperty(action => action.ConcurrencyToken, token), ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception error)
        {
            // A locked intent is itself the fallback. Mule will reclaim it after the
            // configured lock timeout, so completion batching never loses work.
            logger.LogWarning(error,
                "Could not batch-complete {Count} published outbox actions; stale-lock recovery remains armed.",
                ids.Count);
        }
    }
}
