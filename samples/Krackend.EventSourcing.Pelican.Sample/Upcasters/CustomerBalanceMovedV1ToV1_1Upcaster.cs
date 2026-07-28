using System.Text.Json;
using System.Text.Json.Nodes;
using Krackend.EventSourcing.Contracts;
using Krackend.EventSourcing.Upcasting;

namespace Krackend.EventSourcing.Pelican.Sample.Upcasters;

public sealed class CustomerBalanceMovedV1ToV1_1Upcaster : IEventUpcaster
{
    public string EventType => "CustomerBalanceMoved";

    public SemanticVersion FromSchemaVersion => "1.0.0";

    public SemanticVersion ToSchemaVersion => "1.1.0";

    public string Upcast(string payload)
    {
        var node = JsonNode.Parse(payload)
            ?? throw new JsonException("CustomerBalanceMoved payload could not be parsed.");

        node["description"] = "Legacy balance movement";
        return node.ToJsonString();
    }
}
