using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;

namespace Krackend.Sagas.Orchestrations.Web;

/// <summary>
/// Consumer bindings derived from a runtime artifact payload.
/// </summary>
internal sealed class RuntimeArtifactConsumerBindings
{
    /// <summary>
    /// Gets the lifecycle action represented by the artifact type.
    /// </summary>
    public RuntimeArtifactLifecycle Lifecycle { get; init; }

    /// <summary>
    /// Gets the back-channel binding where task responses are consumed.
    /// </summary>
    public RuntimeArtifactConsumerBinding BackChannel { get; init; }

    /// <summary>
    /// Gets trigger bindings that can start orchestration instances.
    /// </summary>
    public IReadOnlyCollection<RuntimeArtifactConsumerBinding> Triggers { get; init; } = Array.Empty<RuntimeArtifactConsumerBinding>();

    /// <summary>
    /// Builds consumer bindings from a materialized runtime artifact.
    /// </summary>
    /// <param name="artifact">Runtime artifact.</param>
    /// <returns>Consumer bindings for the artifact lifecycle.</returns>
    public static RuntimeArtifactConsumerBindings From(RuntimeOrchestrationArtifact artifact)
    {
        var payload = ReadPayload(artifact);
        var version = artifact.Version.ToString();

        return new RuntimeArtifactConsumerBindings
        {
            Lifecycle = GetLifecycle(artifact.ArtifactType),
            BackChannel = ReadBackChannel(artifact, payload, version),
            Triggers = ReadTriggers(payload)
        };
    }

    private static JsonObject ReadPayload(RuntimeOrchestrationArtifact artifact)
        => artifact.ArtifactPayload?.AsObject() ?? new JsonObject();

    private static RuntimeArtifactConsumerBinding ReadBackChannel(RuntimeOrchestrationArtifact artifact, JsonObject payload, string version)
    {
        var topic = ReadString(payload, "BackChannelTopic", "backChannelTopic", "ResponseTopic", "responseTopic");
        if (string.IsNullOrWhiteSpace(topic))
            topic = BuildBackChannelTopic(ReadString(payload, "Domain", "domain"), ReadOrchestrationKey(artifact, payload));

        return new RuntimeArtifactConsumerBinding { Topic = topic, Version = version };
    }

    private static string ReadOrchestrationKey(RuntimeOrchestrationArtifact artifact, JsonObject payload)
    {
        var key = ReadString(payload, "Key", "key", "OrchestrationDefinitionKey", "orchestrationDefinitionKey");
        return string.IsNullOrWhiteSpace(key) ? artifact.OrchestrationDefinitionKey : key;
    }

    private static IReadOnlyCollection<RuntimeArtifactConsumerBinding> ReadTriggers(JsonObject payload)
        => ReadArray(payload, "TriggerBindings", "triggerBindings").Select(ReadTrigger).Where(x => x is not null).ToArray();

    /// <summary>
    /// Reads a trigger binding from the artifact payload when it is enabled and has a topic.
    /// </summary>
    /// <param name="node">Trigger binding JSON node.</param>
    /// <returns>Consumer binding, or null when the trigger should not be registered.</returns>
    private static RuntimeArtifactConsumerBinding ReadTrigger(JsonNode node)
    {
        var binding = node?.AsObject();
        if (binding is null || !ReadBool(binding, true, "IsEnabled", "isEnabled"))
            return null;

        var channel = ReadObject(binding, "TriggerChannel", "triggerChannel");
        if (channel is null)
            return null;

        var topic = ReadString(channel, "Topic", "topic");
        if (string.IsNullOrWhiteSpace(topic))
            return null;

        return new RuntimeArtifactConsumerBinding
        {
            Topic = topic,
            Version = ReadVersion(channel, "Version", "version")
        };
    }

    /// <summary>
    /// Converts runtime artifact type into the consumer lifecycle action.
    /// </summary>
    /// <param name="artifactType">Artifact type from distribution.</param>
    /// <returns>Runtime lifecycle action.</returns>
    private static RuntimeArtifactLifecycle GetLifecycle(string artifactType)
    {
        if (string.Equals(artifactType, "orchestration.deprecate", StringComparison.OrdinalIgnoreCase))
            return RuntimeArtifactLifecycle.Deprecated;

        if (string.Equals(artifactType, "orchestration.archive", StringComparison.OrdinalIgnoreCase))
            return RuntimeArtifactLifecycle.Archived;

        return RuntimeArtifactLifecycle.Deploy;
    }

    /// <summary>
    /// Builds the default orchestration back-channel topic when the artifact does not specify one.
    /// </summary>
    /// <param name="domain">Orchestration domain.</param>
    /// <param name="key">Orchestration key.</param>
    /// <returns>Back-channel topic.</returns>
    private static string BuildBackChannelTopic(string domain, string key)
    {
        var parts = new[] { "orchestrations", domain, key }
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim().Replace(" ", "_"));

        return string.Join(".", parts).ToLowerInvariant();
    }

    private static JsonObject ReadObject(JsonObject obj, params string[] names)
        => names.Select(name => obj[name]).OfType<JsonObject>().FirstOrDefault();

    private static IEnumerable<JsonNode> ReadArray(JsonObject obj, params string[] names)
        => names.Select(name => obj[name]).OfType<JsonArray>().FirstOrDefault() ?? Enumerable.Empty<JsonNode>();

    private static string ReadString(JsonObject obj, params string[] names)
    {
        foreach (var name in names)
        {
            if (obj[name] is JsonValue value && value.TryGetValue<string>(out var result))
                return result ?? string.Empty;
        }

        return string.Empty;
    }

    private static string ReadVersion(JsonObject obj, params string[] names)
    {
        foreach (var name in names)
        {
            var node = obj[name];
            if (node is JsonValue value && value.TryGetValue<string>(out var text))
                return text;

            if (node is JsonObject version)
                return $"{ReadInt(version, "Major", "major")}.{ReadInt(version, "Minor", "minor")}.{ReadInt(version, "Patch", "patch")}";
        }

        return "1.0.0";
    }

    private static int ReadInt(JsonObject obj, params string[] names)
    {
        foreach (var name in names)
        {
            if (obj[name] is JsonValue value && value.TryGetValue<int>(out var result))
                return result;
        }

        return 0;
    }

    private static bool ReadBool(JsonObject obj, bool defaultValue, params string[] names)
    {
        foreach (var name in names)
        {
            if (obj[name] is JsonValue value && value.TryGetValue<bool>(out var result))
                return result;
        }

        return defaultValue;
    }
}
