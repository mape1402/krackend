using System.Text.Json;
namespace Krackend.Demo;

public sealed record DemoLine(string Sku, int Quantity, decimal UnitPrice);
public sealed class DemoRequest
{
    public string RequestId { get; set; } = "";
    public string TestRunId { get; set; } = "";
    public string SaleId { get; set; } = "";
    public string CustomerId { get; set; } = "demo-customer";
    public string Email { get; set; } = "customer@example.test";
    public decimal Total { get; set; }
    public List<DemoLine> Lines { get; set; } = [];
    public List<DemoLine> ReplacementLines { get; set; } = [];
    public DateTime? ExpiresOnUtc { get; set; }
    public string ProviderNotificationId { get; set; } = "";
    public string DelayOperation { get; set; } = "";
    public int DelayMilliseconds { get; set; }
    public string RejectOperation { get; set; } = "";
}
public sealed record DemoCommand(string Operation, DemoRequest Request, System.Text.Json.Nodes.JsonNode? Inputs = null);
public sealed record DemoResult(string OperationId, string RequestId, string SaleId, string Status, string? Error = null,
    System.Text.Json.Nodes.JsonNode? Data = null);
public sealed record DemoCase(string Id, string Key, string Service, string Route, string InputTopic, string EventTopic, string[][] Stages);
public static class DemoJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };
    public static string Write<T>(T value) => JsonSerializer.Serialize(value, Options);
    public static T Read<T>(string value) => JsonSerializer.Deserialize<T>(value, Options) ?? throw new InvalidOperationException("Missing demo payload.");
}
public static class DemoCases
{
    public const string Version = "1.0.0";
    public const string CallbackTopic = "demo.krackend.callbacks";
    public static readonly DemoCase[] All =
    [
        new("UC01", "demo.sale.standard", "sales", "/sales/physical", "requests.sales.physical.create", "events.sales.physical.requested",
            [["sales.create"], ["inventories.reserve"], ["payments.capture"], ["inventories.commit"], ["sales.confirm"], ["email.send"]]),
        new("UC02", "demo.sale.digital", "sales", "/sales/digital", "requests.sales.digital.create", "events.sales.digital.requested",
            [["sales.create"], ["payments.capture"], ["sales.access", "email.send"], ["sales.confirm"]]),
        new("UC03", "demo.sale.reservation", "sales", "/sales/reservations", "requests.sales.reservation.create", "events.sales.reservation.requested",
            [["sales.create"], ["inventories.reserve", "payments.intent"], ["sales.await-payment"], ["email.send"]]),
        new("UC04", "demo.payment.confirmation", "payments", "/payments/confirmations", "requests.payments.confirmation.register", "events.payments.confirmation.received",
            [["payments.confirm"], ["sales.claim-payment"], ["inventories.commit"], ["sales.confirm"], ["email.send"]]),
        new("UC05", "demo.sale.cancellation", "sales", "/sales/{saleId}/cancellations", "requests.sales.cancellation.create", "events.sales.cancellation.requested",
            [["sales.claim-cancellation"], ["inventories.restore", "payments.refund"], ["sales.cancel"], ["email.send"]]),
        new("UC06", "demo.sale.partial-return", "sales", "/sales/{saleId}/returns", "requests.sales.return.create", "events.sales.return.requested",
            [["sales.authorize-return"], ["inventories.return", "payments.refund-partial"], ["sales.return"], ["email.send"]]),
        new("UC07", "demo.sale.exchange", "sales", "/sales/{saleId}/exchanges", "requests.sales.exchange.create", "events.sales.exchange.requested",
            [["sales.authorize-exchange"], ["inventories.reserve-exchange"], ["payments.adjust"], ["inventories.exchange"], ["sales.exchange"], ["email.send"]]),
        new("UC08", "demo.inventory.receipt", "inventories", "/inventories/receipts", "requests.inventories.receipt.register", "events.inventories.receipt.requested",
            [["inventories.receipt"], ["email.send"]]),
        new("UC09", "demo.reservation.expiration", "sales", "/sales/reservations/{reservationId}/expirations", "requests.sales.reservation.expire", "events.sales.reservation.expiration-requested",
            [["sales.claim-expiration"], ["inventories.release", "payments.cancel-intent"], ["sales.expire"], ["email.send"]]),
        new("UC10", "demo.receipt.reissue", "sales", "/sales/{saleId}/receipt-deliveries", "requests.sales.receipt-delivery.create", "events.sales.receipt-delivery.requested",
            [["sales.receipt", "payments.receipt"], ["email.send"], ["sales.communication"]])
    ];
    public static DemoCase Get(string id) => All.Single(x => x.Id == id);
}
public sealed class DemoRejectedException(string message) : Exception(message);
public interface IDemoBusinessStore
{
    Task<DemoResult> ApplyAsync(DemoCommand command, CancellationToken cancellationToken = default);
}

