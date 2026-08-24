using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Infrastructure;

internal sealed class JsonNodeConverter : ValueConverter<JsonNode, string>
{
    public JsonNodeConverter()
        : base(
            node => node == null ? null : node.ToJsonString(),
            value => string.IsNullOrWhiteSpace(value) ? null : JsonNode.Parse(value))
    {
    }
}
