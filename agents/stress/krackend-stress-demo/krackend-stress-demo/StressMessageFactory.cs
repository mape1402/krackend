using System.Text.Json.Nodes;

namespace KrackendStressDemo;

internal sealed class StressMessageFactory
{
    private readonly BatchPublisherOptions _options;
    private readonly string _runId = Guid.NewGuid().ToString("N");

    public StressMessageFactory(BatchPublisherOptions options)
    {
        _options = options;
    }

    public JsonObject Create(string queue, int sequence)
    {
        var payload = new JsonObject
        {
            ["runId"] = _runId,
            ["queue"] = queue,
            ["sequence"] = sequence,
            ["saleId"] = $"sale-{sequence:0000000000}",
            ["createdOnUtc"] = DateTimeOffset.UtcNow,
            ["amount"] = 100 + sequence % 900,
            ["currency"] = "USD"
        };

        if (_options.PayloadBytes > 0)
        {
            payload["padding"] = new string('x', _options.PayloadBytes);
        }

        return payload;
    }
}
