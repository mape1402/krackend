using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;
using Krackend.Sagas.Orchestrations.Client.Publishing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Mule;
using Mule.Dispatching;
using Pigeon.Messaging.Contracts;
using Pigeon.Messaging.Producing;

namespace Krackend.Demo;

public sealed record DemoPublication(string Topic, string PayloadJson, OrchestrationMessageMetadata Metadata,
    OrchestrationExecutionResultMetadata? Result = null);
public sealed record DemoEmailDelivery(string EmailId, DemoPublication Callback);

/// <summary>
/// Persists one publication intent in the business transaction. Successful message
/// publications run directly after commit; Mule only owns failed publication recovery.
/// </summary>
public sealed class DemoOutbox(DemoDbContext db, IMuleSerializer serializer, IMuleCommitNotifier notifier,
    IProducer producer, IOrchestrationClientPublisher replies,
    IOrchestrationMessageMetadataSetter metadata, IOrchestrationExecutionResultMetadataSetter results,
    ILogger<DemoOutbox> logger)
{
    private readonly List<PendingPublication> _pending = [];

    public async Task AddAsync<T>(string actionKey, string key, T payload, string correlationId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(payload);
        var action = ActionKey.From(actionKey);
        var direct = actionKey == "DemoPublish" && payload is DemoPublication;
        if (!direct && await db.Set<DurableAction>().AnyAsync(
                x => x.Key == action && x.DeduplicationKey == key, ct)) return;
        var row = new DurableAction { Key = action, Payload = serializer.Serialize(payload),
            PayloadType = typeof(T).AssemblyQualifiedName!, CorrelationId = correlationId, DeduplicationKey = key,
            Lane = actionKey == "DemoDeliverEmail" ? "demo-email" : "demo-publish",
            Status = DurableActionStatus.Pending,
            Attempts = 0 };
        // Rabbit keeps the incoming command unacknowledged until the handler returns.
        // A healthy direct callback therefore needs no second durable queue record.
        // Only non-direct work is inserted in the business transaction; a failed
        // direct publication is materialized below before the consumer may ACK.
        if (!direct) db.Set<DurableAction>().Add(row);
        _pending.Add(new PendingPublication(row, payload, direct));
    }

    /// <summary>
    /// Publishes a transport-owned orchestration trigger without first creating a
    /// business-database outbox row. Rabbit's publisher confirmation is the durable
    /// acceptance boundary on the healthy path. If that handoff fails, the exact
    /// publication is materialized for Mule recovery before acceptance returns.
    /// Replays are safe because the request correlation is the runtime start key.
    /// </summary>
    public async Task PublishTriggerAsync(
        DemoPublication publication,
        string deduplicationKey,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(publication);
        try
        {
            await PublishDirectAsync(publication, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            await PersistPublicationRecoveryAsync(
                publication,
                deduplicationKey,
                "Direct trigger publication was cancelled.",
                CancellationToken.None);
            throw;
        }
        catch (Exception error)
        {
            logger.LogWarning(error,
                "Direct trigger publication failed for {CorrelationId}; Mule recovery was activated.",
                publication.Metadata.CorrelationId);
            await PersistPublicationRecoveryAsync(
                publication,
                deduplicationKey,
                error.Message,
                ct);
        }
    }

    public async Task NotifyCommittedAsync(CancellationToken ct)
    {
        foreach (var publication in _pending)
        {
            if (!publication.Direct)
            {
                await NotifyMuleBestEffortAsync(publication.Action, ct);
                continue;
            }

            try
            {
                await PublishDirectAsync((DemoPublication)publication.Payload, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                await ReleaseToMuleAsync(publication.Action, "Direct publication was cancelled.", CancellationToken.None);
                throw;
            }
            catch (Exception error)
            {
                logger.LogWarning(error,
                    "Direct publication failed for durable action {ActionId}; Mule recovery was activated.",
                    publication.Action.Id);
                await ReleaseToMuleAsync(publication.Action, error.Message, ct);
            }
        }
        _pending.Clear();
    }

    public void Reset() => _pending.Clear();

    private async Task PublishDirectAsync(DemoPublication work, CancellationToken ct)
    {
        metadata.Set(work.Metadata);
        results.Set(work.Result);
        var body = System.Text.Json.Nodes.JsonNode.Parse(work.PayloadJson)!;
        if (work.Result is not null)
            await replies.PublishAsync(body, work.Metadata.ReplyAddress, ct);
        else
            await producer.PublishAsync(body, work.Topic, SemanticVersion.Parse(DemoCases.Version), ct);
    }

    private async Task ReleaseToMuleAsync(DurableAction action, string error, CancellationToken ct)
    {
        action.Status = DurableActionStatus.Pending;
        action.LockedOnUtc = null;
        action.StartedOnUtc = null;
        action.NextAttemptOnUtc = DateTimeOffset.UtcNow;
        action.LastError = error;
        action.ConcurrencyToken = global::Mule.Ulid.NewUlid();
        if (db.Entry(action).State == EntityState.Detached)
            db.Set<DurableAction>().Add(action);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // A redelivery may have materialized the same failure first. Resolve its
            // durable identity; otherwise propagate so Rabbit redelivers the command.
            db.Entry(action).State = EntityState.Detached;
            var existing = await db.Set<DurableAction>().FirstOrDefaultAsync(x =>
                x.Key == action.Key && x.DeduplicationKey == action.DeduplicationKey, ct);
            if (existing is null) throw;
            action = existing;
        }
        await NotifyMuleBestEffortAsync(action, ct);
    }

    private Task PersistPublicationRecoveryAsync(
        DemoPublication publication,
        string deduplicationKey,
        string error,
        CancellationToken ct)
    {
        var action = new DurableAction
        {
            Key = ActionKey.From("DemoPublish"),
            Payload = serializer.Serialize(publication),
            PayloadType = typeof(DemoPublication).AssemblyQualifiedName!,
            CorrelationId = publication.Metadata.CorrelationId,
            DeduplicationKey = deduplicationKey,
            Lane = "demo-publish",
            Status = DurableActionStatus.Pending,
            Attempts = 0
        };
        return ReleaseToMuleAsync(action, error, ct);
    }

    private async Task NotifyMuleBestEffortAsync(DurableAction action, CancellationToken ct)
    {
        try { await notifier.NotifySavedAsync(action.Id, action.Lane, ct); }
        catch (Exception error)
        {
            logger.LogWarning(error,
                "Immediate recovery activation skipped; Mule will scan durable action {ActionId}.", action.Id);
        }
    }

    private sealed record PendingPublication(DurableAction Action, object Payload, bool Direct);
}
[MuleAction("DemoPublish")]
public sealed class DemoPublishAction(IProducer producer, IOrchestrationClientPublisher replies,
    IOrchestrationMessageMetadataSetter metadata, IOrchestrationExecutionResultMetadataSetter results) : IMuleAction<DemoPublication>
{
    public async ValueTask ExecuteAsync(MuleActionContext<DemoPublication> context, CancellationToken cancellationToken)
    {
        var work = context.Payload;
        metadata.Set(work.Metadata);
        results.Set(work.Result);
        var body = System.Text.Json.Nodes.JsonNode.Parse(work.PayloadJson)!;
        if (work.Result is not null)
            await replies.PublishAsync(body, work.Metadata.ReplyAddress, cancellationToken);
        else
            await producer.PublishAsync(body, work.Topic, SemanticVersion.Parse(DemoCases.Version), cancellationToken);
    }
}

