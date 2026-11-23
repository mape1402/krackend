using System.Text.Json;
using System.Text.Json.Nodes;
using Krackend.Demo;
using Krackend.Demo.RuntimeHost;
using Krackend.Sagas.Orchestrations.Abstractions.Artifacts;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;
using Krackend.Sagas.Orchestrations.Runtime.ButterMorph.DependencyInjection;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Payloads;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Transformations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Mule;
using Mule.Dispatching;

Directory.CreateDirectory("artifacts");
var checkCount = 0;
void Check(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
    checkCount++;
}
var connection = "Data Source=" + Path.GetFullPath("artifacts/demo-domain-" + Guid.NewGuid().ToString("N") + ".db");
DemoDbContext Open() => new(new DbContextOptionsBuilder<DemoDbContext>().UseSqlite(connection).Options);
await using (var db = Open()) await db.Database.EnsureCreatedAsync();
var request = new DemoRequest { RequestId = "request-1", TestRunId = "verification", SaleId = "sale-1",
    Total = 20, Lines = [new("sku-1", 2, 10)] };
await using (var db = Open())
{
    db.Stock.Add(new DemoStock { Id = "sku-1", TestRunId = request.TestRunId, OnHand = 10 });
    await db.SaveChangesAsync();
    var store = new DemoBusinessStore(db);
    var first = await store.ApplyAsync(new("inventories.reserve", request));
    var duplicate = await store.ApplyAsync(new("inventories.reserve", request));
    Check(DemoJson.Write(first) == DemoJson.Write(duplicate), "Duplicate changed its result.");
}
await using (var db = Open())
{
    Check((await db.Stock.SingleAsync()).Reserved == 2, "Duplicate reservation changed stock twice.");
    Check(await db.Operations.CountAsync() == 1, "Duplicate created another operation.");
    Check(await db.Reservations.CountAsync() == 1, "Duplicate created another reservation.");
}
var rejected = new DemoRequest { RequestId = "request-bad", TestRunId = "verification", SaleId = "sale-bad",
    Lines = [new("sku-1", 1, 10), new("sku-missing", 1, 10)] };
await using (var db = Open())
{
    try { await new DemoBusinessStore(db).ApplyAsync(new("inventories.reserve", rejected)); throw new Exception("Missing stock was accepted."); }
    catch (DemoRejectedException) { checkCount++; }
}
await using (var db = Open())
{
    Check((await db.Stock.SingleAsync()).Reserved == 2, "Partial stock mutation survived rollback.");
    Check(!await db.Operations.AnyAsync(x => x.RequestId == rejected.RequestId), "Unfinished business operation survived rollback.");
}
await using (var db = Open())
{
    var outbox = new DemoOutbox(db, new JsonMuleSerializer(), new NoopNotifier(), NullLogger<DemoOutbox>.Instance);
    await using (var tx = await db.Database.BeginTransactionAsync())
    {
        await new DemoBusinessStore(db).ApplyAsync(new("sales.create", request));
        await outbox.AddAsync("DemoPublish", "rollback", new DemoPublication("test", "{}", new()), request.RequestId, default);
        await db.SaveChangesAsync();
        await tx.RollbackAsync();
    }
}
await using (var db = Open())
{
    Check(await db.Sales.CountAsync() == 0, "Business row escaped a rolled-back transaction.");
    Check(await db.Set<DurableAction>().CountAsync() == 0, "Publication intent escaped a rolled-back transaction.");
}
await using (var db = Open())
{
    var handler = new DemoService(db, new DemoBusinessStore(db),
        new DemoOutbox(db, new JsonMuleSerializer(), new NoopNotifier(), NullLogger<DemoOutbox>.Instance),
        new DemoServiceIdentity("sales"), new TestMetadata());
    var accepted = await handler.AcceptAsync("UC01", request);
    var duplicate = await handler.AcceptAsync("UC01", request);
    Check(accepted == duplicate, "Input replay changed its result.");
    Check(await db.Set<DurableAction>().CountAsync() == 1, "Input replay created another trigger.");
}
await using (var db = Open())
{
    var expired = new DemoRequest { RequestId = "already-accepted-expired", TestRunId = "verification", Total = 20,
        SaleId = "already-accepted-expired", Lines = [new("sku-1", 2, 10)], ExpiresOnUtc = DateTime.UtcNow.AddMinutes(-1) };
    var accepted = new DemoResult(expired.RequestId + ":input", expired.RequestId, expired.SaleId, "Accepted");
    db.Operations.Add(new DemoOperation { Id = accepted.OperationId, RequestId = expired.RequestId,
        TestRunId = expired.TestRunId, SaleId = expired.SaleId, Operation = DemoCases.Get("UC03").Key,
        InputHash = DemoBusinessStore.Hash(new { caseId = "UC03", request = expired }),
        ResultJson = DemoJson.Write(accepted), CompletedOnUtc = DateTime.UtcNow.AddMinutes(-5) });
    await db.SaveChangesAsync();
    var handler = new DemoService(db, new DemoBusinessStore(db),
        new DemoOutbox(db, new JsonMuleSerializer(), new NoopNotifier(), NullLogger<DemoOutbox>.Instance),
        new DemoServiceIdentity("sales"), new TestMetadata());
    Check(await handler.AcceptAsync("UC03", expired) == accepted, "An accepted input became invalid when replayed after expiry.");
    expired.RequestId = "new-expired";
    try { await handler.AcceptAsync("UC03", expired); throw new Exception("A new expired reservation was accepted."); }
    catch (System.ComponentModel.DataAnnotations.ValidationException) { checkCount++; }
}
await using (var db = Open())
{
    var handler = new DemoService(db, new DemoBusinessStore(db),
        new DemoOutbox(db, new JsonMuleSerializer(), new NoopNotifier(), NullLogger<DemoOutbox>.Instance),
        new DemoServiceIdentity("inventories"), new TestMetadata());
    await handler.ExecuteAsync(new("inventories.reserve", rejected));
    var operation = await db.Operations.SingleAsync(x => x.RequestId == rejected.RequestId);
    Check(DemoJson.Read<DemoResult>(operation.ResultJson).Status == "Rejected", "Business rejection was not persisted.");
    Check((await db.Stock.SingleAsync()).Reserved == 2, "Rejected consumer left a partial mutation.");
    var reply = await db.Set<DurableAction>().SingleAsync(x => x.DeduplicationKey.StartsWith("reply:"));
    Check(!DemoJson.Read<DemoPublication>(reply.Payload).Result!.Succeeded, "Rejected command produced a success callback.");
}
await using (var db = Open())
{
    await new DemoBusinessStore(db).ApplyAsync(new("inventories.release", new DemoRequest
    { RequestId = "release-1", TestRunId = request.TestRunId, SaleId = request.SaleId, Lines = request.Lines }));
}
await using (var db = Open())
{
    try
    {
        await new DemoBusinessStore(db).ApplyAsync(new("inventories.commit", new DemoRequest
        { RequestId = "late-commit", TestRunId = request.TestRunId, SaleId = request.SaleId, Lines = request.Lines }));
        throw new Exception("Released reservation was committed.");
    }
    catch (DemoRejectedException) { checkCount++; }
}
await using (var db = Open())
{
    await new DemoBusinessStore(db).ApplyAsync(new("sales.create", request));
    var pending = new DemoSale { Id = "racing-sale", Status = "AwaitingPayment", Total = 20, ExpiresOnUtc = DateTime.UtcNow.AddSeconds(-1) };
    db.Sales.Add(pending); await db.SaveChangesAsync();
}
await using (var first = Open())
await using (var stale = Open())
{
    var a = await first.Sales.SingleAsync(x => x.Id == "racing-sale");
    var b = await stale.Sales.SingleAsync(x => x.Id == "racing-sale");
    a.Status = "Paying"; a.Version = Guid.NewGuid(); await first.SaveChangesAsync();
    b.Status = "Expiring"; b.Version = Guid.NewGuid();
    try { await stale.SaveChangesAsync(); throw new Exception("A stale sale resolution committed."); }
    catch (DbUpdateConcurrencyException) { checkCount++; }
}
await using (var db = Open())
{
    var store = new DemoBusinessStore(db);
    await store.ApplyAsync(new("payments.capture", request));
    await store.ApplyAsync(new("payments.refund-partial", new DemoRequest
    { RequestId="refund-1", TestRunId=request.TestRunId, SaleId=request.SaleId, Lines=[new("sku-1",1,10)] }));
}
await using (var db = Open())
{
    try
    {
        await new DemoBusinessStore(db).ApplyAsync(new("payments.refund-partial", new DemoRequest
        { RequestId="refund-too-much", TestRunId=request.TestRunId, SaleId=request.SaleId, Lines=[new("sku-1",2,10)] }));
        throw new Exception("Excessive refund was accepted.");
    }
    catch (DemoRejectedException) { checkCount++; }
}
await using (var db = Open()) Check((await db.Payments.SingleAsync()).Refunded == 10, "Rejected refund changed the balance.");

var services = new ServiceCollection();
services.AddLogging();
services.AddKrackendOrchestrationsRuntimeButterMorph();
await using (var provider = services.BuildServiceProvider())
await using (var scope = provider.CreateAsyncScope())
{
    Check(DemoCases.All.Length == 10 && DemoCases.All.Select(x => x.Key).Distinct().Count() == 10, "Expected ten distinct artifacts.");
    var transform = scope.ServiceProvider.GetRequiredService<IOrchestrationTransformationExecutor>();
    foreach (var definition in DemoCases.All)
    {
        var artifact = DemoArtifactFactory.Create(definition);
        Check(artifact.OrchestrationDefinitionId == DemoArtifactFactory.Create(definition).OrchestrationDefinitionId, "Artifact identity is not stable.");
        foreach (var stage in artifact.StageDefinitions)
        foreach (var task in stage.TaskDefinitions)
        {
            var payload = JsonSerializer.SerializeToNode(request, DemoJson.Options)!;
            var result = await transform.TransformAsync(new OrchestrationTransformationRequest { Task = task,
                PayloadContext = new OrchestrationPayloadContext { TriggerPayload = payload, ContextPayload = new JsonObject(),
                    StageKey = stage.Key, TaskKey = task.Key } });
            Check(result.Succeeded, "DSL failed: " + result.ErrorMessage);
            var command = result.Payload.Deserialize<DemoCommand>(DemoJson.Options)!;
            Check(command.Request.RequestId == request.RequestId && command.Operation == task.Name, "DSL changed the command contract.");
        }
    }
}
// Exercise each case's business steps without claiming a broker/runtime integration run.
foreach (var definition in DemoCases.All)
{
    var saleId = "domain-" + definition.Id;
    var sku = saleId + "-sku";
    var nextSku = saleId + "-next";
    var originalLines = new List<DemoLine> { new(sku, 2, 10) };
    var input = new DemoRequest { RequestId = saleId, SaleId = saleId, TestRunId = "domain-cases", Total = 20,
        Lines = definition.Id is "UC06" or "UC07" ? [new(sku, 1, 10)] : originalLines,
        ReplacementLines = definition.Id == "UC07" ? [new(nextSku, 1, 15)] : [],
        ExpiresOnUtc = definition.Id == "UC09" ? DateTime.UtcNow.AddSeconds(-10) : DateTime.UtcNow.AddMinutes(5) };
    await using (var db = Open())
    {
        db.Stock.AddRange(new DemoStock { Id = sku, OnHand = 1000 }, new DemoStock { Id = nextSku, OnHand = 1000 });
        if (definition.Id is "UC04" or "UC05" or "UC06" or "UC07" or "UC09" or "UC10")
        {
            var pending = definition.Id is "UC04" or "UC09";
            db.Sales.Add(new DemoSale { Id = saleId, Status = pending ? "AwaitingPayment" : "Confirmed",
                Total = 20, LinesJson = DemoJson.Write(originalLines), ExpiresOnUtc = input.ExpiresOnUtc });
            db.Payments.Add(new DemoPayment { Id = saleId, ExpectedTotal = 20, Captured = pending ? 0 : 20,
                IntentStatus = pending ? "Pending" : "Captured" });
            db.Reservations.Add(new DemoReservation { Id = "sale:" + saleId, SaleId = saleId,
                LinesJson = DemoJson.Write(originalLines), Status = pending ? "Reserved" : "Committed" });
            var stock = db.Stock.Local.Single(x => x.Id == sku);
            if (pending) stock.Reserved = 2; else stock.OnHand = 998;
        }
        await db.SaveChangesAsync();
    }
    foreach (var operation in definition.Stages.SelectMany(x => x).Where(x => x != "email.send"))
    {
        await using var db = Open();
        var result = await new DemoBusinessStore(db).ApplyAsync(new(operation, input));
        Check(result.Status == "Succeeded", definition.Id + " failed at " + operation);
    }
    await using (var db = Open())
    {
        var expected = definition.Id switch { "UC03" => "AwaitingPayment", "UC05" => "Cancelled",
            "UC06" => "PartiallyReturned", "UC09" => "Expired", _ => "Confirmed" };
        if (definition.Id != "UC08") Check((await db.Sales.SingleAsync(x => x.Id == saleId)).Status == expected,
            definition.Id + " persisted the wrong sale outcome.");
    }
}
foreach (var confirmationFirst in new[] { true, false })
{
    var saleId = confirmationFirst ? "late-confirm-first" : "late-cancel-first";
    await using (var db = Open())
    {
        db.Payments.Add(new DemoPayment { Id = saleId, ExpectedTotal = 20, IntentStatus = "Pending" });
        await db.SaveChangesAsync();
    }
    var steps = confirmationFirst ? new[] { "payments.confirm", "payments.cancel-intent", "payments.refund" }
        : new[] { "payments.cancel-intent", "payments.confirm", "payments.refund" };
    foreach (var operation in steps)
    {
        await using var db = Open();
        await new DemoBusinessStore(db).ApplyAsync(new(operation, new DemoRequest
        { RequestId = saleId + ":" + operation, SaleId = saleId, TestRunId = "late-payment", Total = 20 }));
    }
    await using (var db = Open())
    {
        var balance = await db.Payments.SingleAsync(x => x.Id == saleId);
        Check(balance.Captured == 20 && balance.Refunded == 20, "Payment/expiry interleaving lost or duplicated the refund.");
        Check(balance.IntentStatus == "Cancelled", "An expired payment intent remained active.");
    }
}
await using (var sql = new DemoDbContext(new DbContextOptionsBuilder<DemoDbContext>()
    .UseSqlServer("Server=localhost;Database=ModelOnly;User Id=unused;Password=unused;TrustServerCertificate=True").Options))
{
    var ddl = sql.Database.GenerateCreateScript();
    Check(ddl.Contains("CREATE TABLE") && ddl.Contains("DurableAction") && ddl.Contains("Demo"), "SQL Server model generation failed.");
    await File.WriteAllTextAsync("artifacts/demo-service-schema.sql", ddl);
}
await File.WriteAllTextAsync("artifacts/demo-domain-checks.json", DemoJson.Write(new { passed = checkCount, failed = 0, provider = "SQLite; SQL Server DDL generation only" }));
Console.WriteLine($"Passed {checkCount} domain, outbox and artifact/DSL checks. Live SQL Server/Rabbit/SMTP verification is separate.");

sealed class NoopNotifier : IMuleCommitNotifier
{
    public ValueTask NotifySavedAsync(Mule.Ulid actionId, string lane, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
}
sealed class TestMetadata : IOrchestrationMessageMetadataAccessor
{
    private readonly OrchestrationMessageMetadata _value = new() { DispatchId = System.Ulid.NewUlid().ToString(), Attempt = 1, CorrelationId = "test",
        ReplyAddress = new() { Transport = "Messaging", SettingsPayload = "{\"topic\":\"test.callbacks\",\"version\":\"1.0.0\"}" } };
    public OrchestrationMessageMetadata Get() => _value;
}

