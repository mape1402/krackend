using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace Krackend.Demo;

public sealed class DemoBusinessStore(DemoDbContext db) : IDemoBusinessStore
{
    public static string Hash(object value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(DemoJson.Write(value))));

    public Task<DemoResult> ApplyAsync(
        DemoCommand command,
        CancellationToken cancellationToken = default)
        => ApplyCoreAsync(command, checkExistingOperation: true, cancellationToken);

    internal Task<DemoResult> ApplyConsumerAsync(
        DemoCommand command,
        bool checkExistingOperation,
        CancellationToken cancellationToken = default)
        => ApplyCoreAsync(command, checkExistingOperation, cancellationToken);

    private async Task<DemoResult> ApplyCoreAsync(
        DemoCommand command,
        bool checkExistingOperation,
        CancellationToken cancellationToken)
    {
        using var suppressedAmbient = System.Transactions.Transaction.Current is not null && db.Database.CurrentTransaction is null
            ? new System.Transactions.TransactionScope(System.Transactions.TransactionScopeOption.Suppress,
                System.Transactions.TransactionScopeAsyncFlowOption.Enabled) : null;
        // New consumers own the transaction that includes their callback. Legacy
        // handlers use a local transaction and retain their existing Spider reply.
        var owned = db.Database.CurrentTransaction is null
            ? await db.Database.BeginTransactionAsync(cancellationToken) : null;
        try
        {
            var r = command.Request;
            var id = r.RequestId + ":" + command.Operation;
            var hash = Hash(command);
            var previous = checkExistingOperation
                ? await db.Operations.FindAsync([id], cancellationToken)
                : null;
            if (previous is not null)
            {
                if (previous.InputHash != hash) throw new DemoRejectedException("An operation ID was reused with different input.");
                return DemoJson.Read<DemoResult>(previous.ResultJson);
            }
            var operation = new DemoOperation { Id = id, RequestId = r.RequestId, TestRunId = r.TestRunId,
                SaleId = r.SaleId, Operation = command.Operation, InputHash = hash };
            db.Operations.Add(operation);
            if (r.RejectOperation == command.Operation) throw new DemoRejectedException("Requested demo business rejection.");
            if (command.Operation.StartsWith("sales.")) await SalesAsync(command, cancellationToken);
            else if (command.Operation.StartsWith("inventories.")) await InventoryAsync(command, cancellationToken);
            else if (command.Operation.StartsWith("payments.")) await PaymentsAsync(command, cancellationToken);
            else throw new DemoRejectedException("Unknown demo operation.");
            object? snapshot = null;
            if (command.Operation.StartsWith("sales."))
            {
                var sale = db.Sales.Local.FirstOrDefault(x => x.Id == r.SaleId);
                if (sale is not null) snapshot = new { sale.Id, sale.CustomerId, sale.Total, sale.Status, Lines = DemoJson.Read<List<DemoLine>>(sale.LinesJson) };
            }
            else if (command.Operation.StartsWith("payments."))
            {
                var payment = db.Payments.Local.FirstOrDefault(x => x.Id == r.SaleId);
                if (payment is not null) snapshot = new { payment.Id, payment.Captured, payment.Refunded, payment.IntentStatus };
            }
            var result = new DemoResult(id, r.RequestId, r.SaleId, "Succeeded", Data:
                snapshot is null ? null : System.Text.Json.JsonSerializer.SerializeToNode(snapshot, DemoJson.Options));
            operation.ResultJson = DemoJson.Write(result);
            operation.CompletedOnUtc = DateTime.UtcNow;
            // The command owner persists the business mutation, idempotency record,
            // and callback together. Legacy callers without an ambient EF transaction
            // still get the same atomic behavior with one database round trip.
            if (owned is not null)
            {
                await db.SaveChangesAsync(cancellationToken);
                await owned.CommitAsync(cancellationToken);
            }
            return result;
        }
        finally { if (owned is not null) await owned.DisposeAsync(); }
    }

    private async Task SalesAsync(DemoCommand c, CancellationToken ct)
    {
        var r = c.Request;
        var sale = await db.Sales.FindAsync([r.SaleId], ct);
        if (c.Operation == "sales.create")
        {
            if (sale is null) db.Sales.Add(new DemoSale { Id = r.SaleId, TestRunId = r.TestRunId, CustomerId = r.CustomerId ?? "demo-customer",
                Total = r.Total, LinesJson = DemoJson.Write(r.Lines), ExpiresOnUtc = r.ExpiresOnUtc, OriginRequestId = r.RequestId });
            else if (sale.OriginRequestId != r.RequestId || sale.Total != r.Total || sale.LinesJson != DemoJson.Write(r.Lines))
                throw new DemoRejectedException("Sale already exists with different details.");
            return;
        }
        if (sale is null) throw new DemoRejectedException("Sale does not exist.");
        switch (c.Operation)
        {
            case "sales.abort":
                if (sale.Status is "Pending" or "Paying") sale.Status = "Rejected";
                break;
            case "sales.release-resolution":
                if (sale.ResolutionRequestId == r.RequestId && sale.Status is "Paying" or "Exchanging" or "Returning")
                { sale.Status = sale.PreviousStatus ?? "Pending"; sale.ResolutionRequestId = null; }
                break;
            case "sales.confirm":
                if (sale.Status is not ("Pending" or "Paying" or "Confirmed")) throw new DemoRejectedException("Sale cannot be confirmed.");
                sale.Status = "Confirmed"; break;
            case "sales.await-payment":
                if (sale.Status != "Pending") throw new DemoRejectedException("Sale is no longer pending.");
                sale.Status = "AwaitingPayment"; break;
            case "sales.claim-payment":
                if (r.Total != sale.Total) throw new DemoRejectedException("Payment amount does not match sale.");
                Claim(sale, r, "Paying", ["AwaitingPayment"]); break;
            case "sales.claim-expiration":
                if (sale.ExpiresOnUtc is null || sale.ExpiresOnUtc > DateTime.UtcNow) throw new DemoRejectedException("Reservation has not expired.");
                Claim(sale, r, "Expiring", ["AwaitingPayment"]); break;
            case "sales.claim-cancellation": Claim(sale, r, "Cancelling", ["Pending", "AwaitingPayment", "Confirmed"]); break;
            case "sales.cancel": RequireOwner(sale, r, "Cancelling"); sale.Status = "Cancelled"; break;
            case "sales.expire": RequireOwner(sale, r, "Expiring"); sale.Status = "Expired"; break;
            case "sales.authorize-return":
            case "sales.authorize-exchange":
                ValidateReturn(sale, r);
                Claim(sale, r, c.Operation.EndsWith("exchange") ? "Exchanging" : "Returning", ["Confirmed", "Delivered", "PartiallyReturned"]); break;
            case "sales.return":
                RequireOwner(sale, r, "Returning");
                var returned = DemoJson.Read<List<DemoLine>>(sale.ReturnedLinesJson).Concat(r.Lines)
                    .GroupBy(x => x.Sku).Select(x => new DemoLine(x.Key, x.Sum(y => y.Quantity), x.First().UnitPrice)).ToList();
                sale.ReturnedLinesJson = DemoJson.Write(returned);
                sale.ReturnedAmount += Value(r.Lines);
                sale.Status = sale.ReturnedAmount == sale.Total ? "Returned" : "PartiallyReturned"; break;
            case "sales.exchange":
                RequireOwner(sale, r, "Exchanging");
                var lines = DemoJson.Read<List<DemoLine>>(sale.LinesJson);
                foreach (var line in r.Lines)
                {
                    var original = lines.Single(x => x.Sku == line.Sku);
                    lines.Remove(original);
                    if (original.Quantity > line.Quantity) lines.Add(original with { Quantity = original.Quantity - line.Quantity });
                }
                lines.AddRange(r.ReplacementLines);
                sale.LinesJson = DemoJson.Write(lines.GroupBy(x => new { x.Sku, x.UnitPrice })
                    .Select(x => new DemoLine(x.Key.Sku, x.Sum(y => y.Quantity), x.Key.UnitPrice)).ToList());
                sale.Total += Value(r.ReplacementLines) - Value(r.Lines); sale.Status = "Confirmed"; break;
            case "sales.access": AddEntry("access:" + sale.Id, "DigitalAccess", r); break;
            case "sales.communication": AddEntry(r.RequestId + ":communication", "Communication", r); break;
            case "sales.receipt": break;
            default: throw new DemoRejectedException("Unknown Sales operation.");
        }
        sale.Version = Guid.NewGuid();
    }

    private static void Claim(DemoSale sale, DemoRequest request, string state, string[] allowed)
    {
        if (sale.Status == state && sale.ResolutionRequestId == request.RequestId) return;
        if (!allowed.Contains(sale.Status)) throw new DemoRejectedException("Sale state prevents this resolution: " + sale.Status);
        sale.PreviousStatus = sale.Status; sale.Status = state; sale.ResolutionRequestId = request.RequestId;
    }
    private static void RequireOwner(DemoSale sale, DemoRequest request, string state)
    {
        if (sale.Status != state || sale.ResolutionRequestId != request.RequestId)
            throw new DemoRejectedException("This operation does not own the business resolution.");
    }
    private static void ValidateReturn(DemoSale sale, DemoRequest r)
    {
        var sold = DemoJson.Read<List<DemoLine>>(sale.LinesJson);
        var returned = DemoJson.Read<List<DemoLine>>(sale.ReturnedLinesJson);
        if (r.Lines.Count == 0) throw new DemoRejectedException("Return lines are required.");
        foreach (var line in r.Lines)
        {
            var original = sold.SingleOrDefault(x => x.Sku == line.Sku);
            if (original is null || original.UnitPrice != line.UnitPrice || line.Quantity <= 0 ||
                line.Quantity + returned.Where(x => x.Sku == line.Sku).Sum(x => x.Quantity) > original.Quantity)
                throw new DemoRejectedException("Return exceeds the original sale or uses a different price.");
        }
    }

    private async Task InventoryAsync(DemoCommand c, CancellationToken ct)
    {
        var r = c.Request;
        var exchange = c.Operation.Contains("exchange");
        var reservationId = exchange ? "exchange:" + r.RequestId : "sale:" + r.SaleId;
        var reservation = await db.Reservations.FindAsync([reservationId], ct);
        if (c.Operation is "inventories.reserve" or "inventories.reserve-exchange")
        {
            if (reservation is not null)
            {
                if (reservation.Status is not ("Reserved" or "Committed")) throw new DemoRejectedException("Reservation is no longer usable.");
                return;
            }
            var lines = exchange ? r.ReplacementLines : r.Lines;
            if (lines.Count == 0) throw new DemoRejectedException("Stock lines are required.");
            foreach (var line in lines.OrderBy(x => x.Sku))
            {
                var changed = await db.Stock.Where(x => x.Id == line.Sku && x.OnHand - x.Reserved >= line.Quantity)
                    .ExecuteUpdateAsync(u => u.SetProperty(x => x.Reserved, x => x.Reserved + line.Quantity), ct);
                if (changed != 1) throw new DemoRejectedException("Insufficient stock: " + line.Sku);
            }
            db.Reservations.Add(new DemoReservation { Id = reservationId, SaleId = r.SaleId, TestRunId = r.TestRunId,
                LinesJson = DemoJson.Write(lines), ExpiresOnUtc = r.ExpiresOnUtc });
        }
        else if (c.Operation is "inventories.commit" or "inventories.release" or "inventories.release-exchange" or "inventories.restore" or "inventories.exchange")
        {
            if (reservation is null)
            {
                if (c.Operation is "inventories.release" or "inventories.release-exchange" or "inventories.restore") return;
                throw new DemoRejectedException("Reservation is missing.");
            }
            var commit = c.Operation is "inventories.commit" or "inventories.exchange";
            if ((commit && reservation.Status == "Committed") || (!commit && reservation.Status == "Released")) return;
            if (commit && reservation.Status != "Reserved") throw new DemoRejectedException("Cannot commit a released reservation.");
            foreach (var line in DemoJson.Read<List<DemoLine>>(reservation.LinesJson).OrderBy(x => x.Sku))
            {
                int changed;
                if (commit)
                    changed = await db.Stock.Where(x => x.Id == line.Sku && x.Reserved >= line.Quantity && x.OnHand >= line.Quantity)
                        .ExecuteUpdateAsync(u => u.SetProperty(x => x.Reserved, x => x.Reserved - line.Quantity)
                            .SetProperty(x => x.OnHand, x => x.OnHand - line.Quantity), ct);
                else if (reservation.Status == "Reserved")
                    changed = await db.Stock.Where(x => x.Id == line.Sku && x.Reserved >= line.Quantity)
                        .ExecuteUpdateAsync(u => u.SetProperty(x => x.Reserved, x => x.Reserved - line.Quantity), ct);
                else if (c.Operation == "inventories.restore" && reservation.Status == "Committed")
                    changed = await db.Stock.Where(x => x.Id == line.Sku)
                        .ExecuteUpdateAsync(u => u.SetProperty(x => x.OnHand, x => x.OnHand + line.Quantity), ct);
                else throw new DemoRejectedException("Invalid reservation state.");
                if (changed != 1) throw new DemoRejectedException("Stock changed incompatibly.");
            }
            reservation.Status = commit ? "Committed" : "Released"; reservation.Version = Guid.NewGuid();
            if (c.Operation == "inventories.exchange") await AddStockAsync(r.Lines, ct);
        }
        else if (c.Operation is "inventories.return" or "inventories.receipt") await AddStockAsync(r.Lines, ct);
        else throw new DemoRejectedException("Unknown Inventory operation.");
        AddEntry(r.RequestId + ":" + c.Operation, c.Operation == "inventories.receipt" ? "GoodsReceipt" : "StockMovement", r);
    }
    private async Task AddStockAsync(List<DemoLine> lines, CancellationToken ct)
    {
        foreach (var line in lines.OrderBy(x => x.Sku))
            if (await db.Stock.Where(x => x.Id == line.Sku).ExecuteUpdateAsync(u =>
                u.SetProperty(x => x.OnHand, x => x.OnHand + line.Quantity), ct) != 1)
                throw new DemoRejectedException("Seed the SKU before receiving it: " + line.Sku);
    }

    private async Task PaymentsAsync(DemoCommand c, CancellationToken ct)
    {
        var r = c.Request;
        var payment = await db.Payments.FindAsync([r.SaleId], ct);
        if (payment is null)
        {
            if (c.Operation is "payments.refund" or "payments.cancel-intent") return;
            if (c.Operation is not ("payments.capture" or "payments.intent")) throw new DemoRejectedException("Payment does not exist.");
            payment = new DemoPayment { Id = r.SaleId, TestRunId = r.TestRunId }; db.Payments.Add(payment);
        }
        switch (c.Operation)
        {
            case "payments.intent":
                if (payment.IntentStatus == "Cancelled") throw new DemoRejectedException("Payment intent is cancelled.");
                payment.IntentStatus = "Pending"; payment.ExpectedTotal = r.Total; break;
            case "payments.capture":
            case "payments.confirm":
                if (payment.Captured != 0) throw new DemoRejectedException("Payment is already captured.");
                if (payment.ExpectedTotal != 0 && payment.ExpectedTotal != r.Total) throw new DemoRejectedException("Payment amount differs from intent.");
                if (c.Operation == "payments.capture" && payment.IntentStatus == "Cancelled") throw new DemoRejectedException("Payment intent is cancelled.");
                payment.Captured = r.Total; payment.IntentStatus = "Captured"; break;
            case "payments.cancel-intent":
                // Sales has already claimed expiration. A provider confirmation
                // racing this step must be returned, whichever local write wins.
                payment.Refunded = payment.Captured;
                payment.IntentStatus = "Cancelled"; break;
            case "payments.refund": payment.Refunded = payment.Captured; payment.IntentStatus = "Cancelled"; break;
            case "payments.refund-partial":
                payment.Refunded += Value(r.Lines); break;
            case "payments.adjust":
                var difference = Value(r.ReplacementLines) - Value(r.Lines);
                if (difference > 0) payment.Captured += difference; else payment.Refunded -= difference;
                break;
            case "payments.receipt": break;
            default: throw new DemoRejectedException("Unknown Payment operation.");
        }
        if (payment.Refunded > payment.Captured || payment.Refunded < 0) throw new DemoRejectedException("Refund exceeds captured payment.");
        payment.Version = Guid.NewGuid();
        AddEntry(r.RequestId + ":" + c.Operation, c.Operation.Contains("refund") ? "Refund" : "PaymentEntry", r);
    }
    public void AddEntry(string id, string kind, DemoRequest r)
        => db.Entries.Add(new DemoBusinessEntry { Id = id, TestRunId = r.TestRunId, SaleId = r.SaleId, Kind = kind, DataJson = DemoJson.Write(r) });
    public static decimal Value(IEnumerable<DemoLine> lines) => lines.Sum(x => x.Quantity * x.UnitPrice);
}
