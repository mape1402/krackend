using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Mule;

namespace Krackend.Sagas.Orchestrations.Runtime.Buffering.Mule;

internal sealed class MuleRuntimeExecutionKeyOptionsConfigurer : IConfigureOptions<MuleSettings>
{
    public void Configure(MuleSettings options)
    {
        var fallback = options.ExecutionKeySelector;
        options.ExecutionKeySelector = action => Resolve(action) ?? fallback?.Invoke(action);
    }

    internal static ExecutionKey? Resolve(DurableAction action)
    {
        if (action is null) return null;
        var key = action.Key.ToString();
        if (key == MuleActionKeys.TriggerAction)
        {
            var identity = string.IsNullOrWhiteSpace(action.DeduplicationKey)
                ? action.Id.ToString() : action.DeduplicationKey.Trim();
            return ExecutionKey.From("krackend:start:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity))));
        }
        if (key != MuleActionKeys.BackchannelAction && key != MuleActionKeys.TaskResponseTimeoutAction &&
            key != MuleActionKeys.RemoteCommandDispatchAction) return null;
        if (string.IsNullOrWhiteSpace(action.Payload)) return null;

        try
        {
            using var document = JsonDocument.Parse(action.Payload);
            var root = document.RootElement;
            // A physical dispatch has its own identity so parallel commands of a
            // saga do not serialize their network sends behind a global saga key.
            if (key == MuleActionKeys.RemoteCommandDispatchAction)
                return ReadKey(root, "DispatchId", "krackend:dispatch:");
            if (key == MuleActionKeys.BackchannelAction)
            {
                if (!TryProperty(root, "MessageMetadata", out root)) return null;
            }
            return ReadKey(root, "OrchestrationInstanceId", "krackend:instance:");
        }
        catch (JsonException) { return null; } // Invalid payloads retain their normal functional failure handling.
    }

    private static ExecutionKey? ReadKey(JsonElement root, string property, string prefix)
        => TryProperty(root, property, out var value) && value.ValueKind == JsonValueKind.String &&
            System.Ulid.TryParse(value.GetString(), out var id) ? ExecutionKey.From(prefix + id) : null;

    private static bool TryProperty(JsonElement root, string name, out JsonElement value)
    {
        if (root.ValueKind == JsonValueKind.Object)
            foreach (var property in root.EnumerateObject())
                if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                { value = property.Value; return true; }
        value = default;
        return false;
    }
}
