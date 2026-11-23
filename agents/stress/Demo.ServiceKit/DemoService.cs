using System.ComponentModel.DataAnnotations;
using System.Collections.Concurrent;
using System.Net.Mail;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Krackend.Demo;

public sealed record DemoServiceIdentity(string Name);
public sealed class DemoTriggerAcceptanceCache
{
    private const int Capacity = 10_000;
    private readonly ConcurrentDictionary<string, Entry> _entries = new(StringComparer.Ordinal);
    private readonly ConcurrentQueue<string> _insertionOrder = new();

    public async Task<DemoResult> GetOrAcceptAsync(
        string id,
        string inputHash,
        Func<Task<DemoResult>> accept)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(inputHash);
        ArgumentNullException.ThrowIfNull(accept);

        var candidate = new Entry(
            inputHash,
            new Lazy<Task<DemoResult>>(
                accept,
                LazyThreadSafetyMode.ExecutionAndPublication));
        var entry = _entries.GetOrAdd(id, candidate);
        if (!string.Equals(entry.InputHash, inputHash, StringComparison.Ordinal))
            throw new ValidationException("RequestId was already used with different input.");

        if (ReferenceEquals(entry, candidate))
        {
            _insertionOrder.Enqueue(id);
            Trim();
        }

        try
        {
            return await entry.Result.Value;
        }
        catch
        {
            ((ICollection<KeyValuePair<string, Entry>>)_entries)
                .Remove(new KeyValuePair<string, Entry>(id, entry));
            throw;
        }
    }

    private void Trim()
    {
        while (_entries.Count > Capacity && _insertionOrder.TryDequeue(out var oldest))
            _entries.TryRemove(oldest, out _);
    }

    private sealed record Entry(string InputHash, Lazy<Task<DemoResult>> Result);
}

public sealed class DemoService(DemoDbContext db, DemoBusinessStore business, DemoOutbox outbox,
    DemoEmailDeliveryExecutor emailDelivery, DemoServiceIdentity identity,
    IOrchestrationMessageMetadataAccessor metadata, ILogger<DemoService> logger,
    DemoTriggerAcceptanceCache triggerAcceptances,
    DemoCommandObservations? observations = null)
{
    public async Task<DemoResult> AcceptAsync(string caseId, DemoRequest request, CancellationToken ct = default, bool viaConsumer = false)
    {
        using var ambient = new System.Transactions.TransactionScope(System.Transactions.TransactionScopeOption.Suppress,
            System.Transactions.TransactionScopeAsyncFlowOption.Enabled);
        var definition = DemoCases.Get(caseId);
        if (definition.Service != identity.Name) throw new ValidationException("Entry does not belong to this service.");
        Validate(request);
        ValidateEmail(request.Email);
        if ((caseId is "UC01" or "UC02" or "UC03") && request.Total <= 0)
            throw new ValidationException("A positive sale total is required.");
        if ((caseId is "UC01" or "UC03" or "UC06" or "UC07" or "UC08") && request.Lines.Count == 0)
            throw new ValidationException("Business lines are required.");
        if ((caseId is "UC01" or "UC02" or "UC03") && DemoBusinessStore.Value(request.Lines) != request.Total)
            throw new ValidationException("Sale total must equal its lines.");
        if (caseId == "UC07" && request.ReplacementLines.Count == 0)
            throw new ValidationException("Replacement lines are required.");
        var id = request.RequestId + (viaConsumer ? ":transport-input" : ":input");
        var inputHash = DemoBusinessStore.Hash(new { caseId, request });
        if (viaConsumer)
        {
            // The broker is the durable owner of a transport trigger. Do not turn
            // trigger intake into a business SQL transaction plus a broker publish;
            // that doubles the operation and serializes unrelated starts on the SQL
            // transaction log. A failed publish is persisted for Mule recovery by
            // PublishTriggerAsync, and the runtime start key makes replays idempotent.
            return await triggerAcceptances.GetOrAcceptAsync(id, inputHash, async () =>
            {
                var accepted = new DemoResult(id, request.RequestId, request.SaleId, "Accepted");
                await outbox.PublishTriggerAsync(
                    new DemoPublication(
                        definition.InputTopic,
                        DemoJson.Write(request),
                        new OrchestrationMessageMetadata { CorrelationId = request.RequestId }),
                    "transport-input:" + request.RequestId,
                    ct);
                return accepted;
            });
        }

        for (var attempt = 0; ; attempt++)
        {
            try
            {
                await using var tx = await db.Database.BeginTransactionAsync(ct);
                // New transport requests dominate the healthy path. Let the primary
                // key arbitrate the rare redelivery instead of paying a guaranteed-miss
                // SELECT before every insert. A duplicate retries this transaction and
                // reads the committed idempotent result below.
                // UC03 has a time-sensitive expiry check and UC04 has a durable
                // provider-notification uniqueness check. On a broker redelivery,
                // both checks can reject before the operation insert reaches its PK
                // and activates the optimistic duplicate retry. Preflight only those
                // two cases; all other healthy inputs retain the insert-first path.
                var requiresStatefulIdempotencyPreflight = caseId is "UC03" or "UC04";
                var previous = attempt == 0 && !requiresStatefulIdempotencyPreflight
                    ? null
                    : await db.Operations.FindAsync([id], ct);
                if (previous is not null)
                {
                    if (previous.InputHash != inputHash) throw new ValidationException("RequestId was already used with different input.");
                    return DemoJson.Read<DemoResult>(previous.ResultJson);
                }
                if (caseId == "UC03" && (request.ExpiresOnUtc is null || request.ExpiresOnUtc <= DateTime.UtcNow))
                    throw new ValidationException("A future reservation expiration is required.");
                if (!viaConsumer && caseId == "UC04")
                {
                    if (string.IsNullOrWhiteSpace(request.ProviderNotificationId)) throw new ValidationException("ProviderNotificationId is required.");
                    var notificationId = "provider:" + request.ProviderNotificationId;
                    if (await db.Entries.AnyAsync(x => x.Id == notificationId, ct))
                        throw new ValidationException("Provider notification was already accepted with another RequestId.");
                    business.AddEntry(notificationId, "ProviderNotification", request);
                }
                var result = new DemoResult(id, request.RequestId, request.SaleId, "Accepted");
                db.Operations.Add(new DemoOperation { Id = id, RequestId = request.RequestId, TestRunId = request.TestRunId,
                    SaleId = request.SaleId, Operation = definition.Key, InputHash = inputHash,
                    ResultJson = DemoJson.Write(result), CompletedOnUtc = DateTime.UtcNow });
                await outbox.AddAsync("DemoPublish", "input:" + request.RequestId,
                    new DemoPublication(definition.InputTopic, DemoJson.Write(request),
                        new OrchestrationMessageMetadata { CorrelationId = request.RequestId }), request.RequestId, ct);
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                await outbox.NotifyCommittedAsync(ct);
                return result;
            }
            catch (DbUpdateException e) when (attempt < 4 && IsConcurrent(e))
            { db.ChangeTracker.Clear(); outbox.Reset(); await Task.Delay(10 * (attempt + 1), ct); }
        }
    }

    public async Task ExecuteAsync(DemoCommand command, CancellationToken ct = default)
    {
        using var ambient = new System.Transactions.TransactionScope(System.Transactions.TransactionScopeOption.Suppress,
            System.Transactions.TransactionScopeAsyncFlowOption.Enabled);
        Validate(command.Request);
        if (!command.Operation.StartsWith(identity.Name + ".", StringComparison.Ordinal))
            throw new ValidationException("Command does not belong to this service.");
        var original = metadata.Get() ?? throw new ValidationException("Orchestration metadata is required.");
        if (original.ReplyAddress is null) throw new ValidationException("The command must have a reply address.");
        observations?.Record(command.Request.RequestId, command.Operation, "Entered");
        // A controlled delay belongs outside SQL transactions and affects only this command.
        if (command.Request.DelayOperation == command.Operation && command.Request.DelayMilliseconds > 0)
            await Task.Delay(Math.Min(command.Request.DelayMilliseconds, 30000), ct);
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                var directEmail = await ExecuteTransactionAsync(
                    command,
                    original,
                    checkExistingOperation: attempt > 0,
                    ct: ct);
                await outbox.NotifyCommittedAsync(ct);
                if (directEmail is not null)
                    await DeliverEmailDirectAsync(directEmail, ct);
                observations?.Record(command.Request.RequestId, command.Operation, "Finished");
                return;
            }
            catch (DbUpdateException e) when (attempt < 4 && IsConcurrent(e))
            { db.ChangeTracker.Clear(); outbox.Reset(); await Task.Delay(10 * (attempt + 1), ct); }
        }
    }

    private async Task<DemoEmailDelivery?> ExecuteTransactionAsync(
        DemoCommand command,
        OrchestrationMessageMetadata original,
        bool checkExistingOperation,
        CancellationToken ct)
    {
        DemoRejectedException? rejection = null;
        await using (var tx = await db.Database.BeginTransactionAsync(ct))
        {
            try
            {
                DemoEmailDelivery? directEmail = null;
                if (command.Operation == "email.send")
                {
                    var id = command.Request.RequestId + ":email";
                    var email = await db.Emails.FindAsync([id], ct);
                    if (email is null)
                    {
                        ValidateEmail(command.Request.Email);
                        if (command.Request.RejectOperation == command.Operation) throw new DemoRejectedException("Requested Email rejection.");
                        db.Emails.Add(new DemoEmail { Id = id, TestRunId = command.Request.TestRunId,
                            SaleId = command.Request.SaleId, Recipient = command.Request.Email, Body = DemoJson.Write(new { command.Request, command.Inputs }) });
                    }
                    var result = new DemoResult(id, command.Request.RequestId, command.Request.SaleId, "Succeeded");
                    var callback = Callback(original, result);
                    if (email?.Status == "Delivered")
                        await QueueCallbackAsync(callback, ct);
                    else
                        directEmail = new DemoEmailDelivery(id, callback);
                }
                else
                {
                    var result = await business.ApplyConsumerAsync(
                        command,
                        checkExistingOperation,
                        ct);
                    await QueueCallbackAsync(Callback(original, result), ct);
                }
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return directEmail;
            }
            catch (DemoRejectedException error) { rejection = error; }
        }
        db.ChangeTracker.Clear();
        outbox.Reset();
        // Rejecting a business command rolls back every partial local mutation,
        // then persists the rejection reply so redelivery cannot lose the outcome.
        await using var rejectedTx = await db.Database.BeginTransactionAsync(ct);
        var failed = new DemoResult(command.Request.RequestId + ":" + command.Operation,
            command.Request.RequestId, command.Request.SaleId, "Rejected", rejection!.Message);
        var previous = await db.Operations.FindAsync([failed.OperationId], ct);
        if (previous is null)
            db.Operations.Add(new DemoOperation { Id = failed.OperationId, RequestId = failed.RequestId,
                SaleId = failed.SaleId, TestRunId = command.Request.TestRunId, Operation = command.Operation,
                InputHash = DemoBusinessStore.Hash(command), ResultJson = DemoJson.Write(failed), CompletedOnUtc = DateTime.UtcNow });
        await QueueCallbackAsync(Callback(original, failed), ct);
        await db.SaveChangesAsync(ct);
        await rejectedTx.CommitAsync(ct);
        return null;
    }

    private async Task DeliverEmailDirectAsync(DemoEmailDelivery delivery, CancellationToken ct)
    {
        try
        {
            await emailDelivery.ExecuteAsync(delivery, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            await QueueEmailRecoveryAsync(delivery, "Direct email delivery was cancelled.", CancellationToken.None);
            throw;
        }
        catch (Exception error)
        {
            logger.LogWarning(error,
                "Direct email delivery failed for {EmailId}; Mule recovery was activated.",
                delivery.EmailId);
            await QueueEmailRecoveryAsync(delivery, error.Message, ct);
        }
    }

    private async Task QueueEmailRecoveryAsync(
        DemoEmailDelivery delivery,
        string error,
        CancellationToken ct)
    {
        await outbox.AddAsync("DemoDeliverEmail", "email:" + delivery.EmailId,
            delivery, delivery.Callback.Metadata.CorrelationId, ct);
        var action = db.Set<Mule.DurableAction>().Local.LastOrDefault(x =>
            x.Key == Mule.ActionKey.From("DemoDeliverEmail") &&
            x.DeduplicationKey == "email:" + delivery.EmailId);
        if (action is not null)
            action.LastError = error;
        await db.SaveChangesAsync(ct);
        await outbox.NotifyCommittedAsync(ct);
    }
    public Task QueueCallbackAsync(DemoPublication callback, CancellationToken ct)
        => outbox.AddAsync("DemoPublish", "reply:" + callback.Metadata.DispatchId + ":" + callback.Metadata.Attempt,
            callback, callback.Metadata.CorrelationId, ct);

    public static DemoPublication Callback(OrchestrationMessageMetadata original, DemoResult result)
        => new("", DemoJson.Write(result), DemoJson.Read<OrchestrationMessageMetadata>(DemoJson.Write(original)),
            new OrchestrationExecutionResultMetadata { Succeeded = result.Status == "Succeeded",
                Status = result.Status == "Succeeded" ? "Succeeded" : "Failed", ErrorCode = result.Error is null ? null : "DEMO_REJECTED",
                ErrorMessage = result.Error, CompletedOnUtc = DateTime.UtcNow });

    public static void Validate(DemoRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.RequestId) || r.RequestId.Length > 96 ||
            string.IsNullOrWhiteSpace(r.TestRunId) || r.TestRunId.Length > 96)
            throw new ValidationException("RequestId and TestRunId (up to 96 characters) are required.");
        if (string.IsNullOrWhiteSpace(r.SaleId)) r.SaleId = r.RequestId;
        if (r.SaleId.Length > 96 || r.Total < 0 || decimal.Round(r.Total, 2) != r.Total ||
            r.ProviderNotificationId.Length > 120 || r.DelayMilliseconds < 0 || r.DelayMilliseconds > 30000)
            throw new ValidationException("Invalid identifier, amount or delay.");
        foreach (var lines in new[] { r.Lines, r.ReplacementLines })
        {
            if (lines.Count > 100 || lines.Any(x => string.IsNullOrWhiteSpace(x.Sku) || x.Sku.Length > 96 ||
                x.Quantity <= 0 || x.UnitPrice < 0 || decimal.Round(x.UnitPrice, 2) != x.UnitPrice) ||
                lines.Select(x => x.Sku).Distinct().Count() != lines.Count)
                throw new ValidationException("Use up to 100 unique SKU lines with positive quantities and valid prices.");
            lines.Sort((a, b) => string.CompareOrdinal(a.Sku, b.Sku));
        }
    }
    public static void ValidateEmail(string recipient)
    {
        MailAddress address;
        try { address = new MailAddress(recipient); }
        catch (FormatException) { throw new ValidationException("Invalid demo Email address."); }
        if (!address.Host.EndsWith(".test", StringComparison.OrdinalIgnoreCase) && address.Host != "localhost")
            throw new ValidationException("Demo email recipients must use a .test domain or localhost.");
    }
    public static bool IsConcurrent(DbUpdateException e)
        => e is DbUpdateConcurrencyException || e.InnerException is SqlException { Number: 2601 or 2627 or 1205 };
}

