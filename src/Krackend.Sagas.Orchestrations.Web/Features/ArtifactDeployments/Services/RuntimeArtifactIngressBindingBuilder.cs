using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Engine;

namespace Krackend.Sagas.Orchestrations.Web;

/// <summary>
/// Builds runtime ingress bindings using the deployable orchestration artifact contract.
/// </summary>
public sealed class RuntimeArtifactIngressBindingBuilder : IRuntimeArtifactIngressBindingBuilder
{
    /// <inheritdoc/>
    public RuntimeArtifactIngressBindingSet Build(RuntimeOrchestrationArtifact artifact)
    {
        if (artifact is null)
            throw new ArgumentNullException(nameof(artifact));

        var payload = artifact.ArtifactPayload?.AsObject()
            ?? throw new InvalidOperationException("Runtime artifact payload must contain an orchestration artifact.");

        var orchestrationKey = FirstNonEmpty(
            ReadString(payload, "Key", "key", "OrchestrationDefinitionKey", "orchestrationDefinitionKey"),
            artifact.OrchestrationDefinitionKey);
        var orchestrationVersion = artifact.Version.ToString();
        var skipped = new List<RuntimeSkippedIngressBinding>();
        var triggers = new List<RuntimeMessagingIngressBinding>();

        foreach (var bindingNode in ReadArray(payload, "TriggerBindings", "triggerBindings"))
        {
            var binding = bindingNode as JsonObject;
            if (binding is null)
            {
                skipped.Add(Skip(orchestrationKey, "Trigger binding is not a JSON object."));
                continue;
            }

            if (!ReadBool(binding, true, "IsEnabled", "isEnabled"))
            {
                skipped.Add(Skip(orchestrationKey, "Trigger binding is disabled."));
                continue;
            }

            var channel = ReadObject(binding, "TriggerChannel", "triggerChannel");
            if (channel is null)
            {
                skipped.Add(Skip(orchestrationKey, "Trigger binding channel is required."));
                continue;
            }

            var artifactType = ReadString(channel, "$artifactType", "ArtifactType", "artifactType");
            if (!string.IsNullOrWhiteSpace(artifactType) && !string.Equals(artifactType, "event", StringComparison.OrdinalIgnoreCase))
            {
                skipped.Add(Skip(orchestrationKey, $"Trigger binding channel '{artifactType}' is not supported by messaging ingress."));
                continue;
            }

            var topic = ReadString(channel, "Topic", "topic");
            if (string.IsNullOrWhiteSpace(topic))
            {
                skipped.Add(Skip(orchestrationKey, "Messaging event trigger topic is required."));
                continue;
            }

            var version = ReadVersion(channel, "Version", "version");
            if (string.IsNullOrWhiteSpace(version))
                version = "1.0.0";

            triggers.Add(new RuntimeMessagingIngressBinding
            {
                ArtifactId = artifact.Id.ToString(),
                OrchestrationKey = orchestrationKey,
                OrchestrationVersion = orchestrationVersion,
                Kind = RuntimeIngressBindingKind.Trigger,
                Topic = NormalizeTopic(topic),
                Version = version
            });
        }

        return new RuntimeArtifactIngressBindingSet
        {
            OrchestrationKey = orchestrationKey,
            OrchestrationVersion = orchestrationVersion,
            BackChannel = new RuntimeMessagingIngressBinding
            {
                ArtifactId = artifact.Id.ToString(),
                OrchestrationKey = orchestrationKey,
                OrchestrationVersion = orchestrationVersion,
                Kind = RuntimeIngressBindingKind.BackChannel,
                Topic = RuntimeBackChannelTopic.Build(orchestrationKey),
                Version = orchestrationVersion
            },
            MessagingTriggers = triggers,
            Skipped = skipped
        };
    }

    private static RuntimeSkippedIngressBinding Skip(string orchestrationKey, string reason)
        => new()
        {
            OrchestrationKey = orchestrationKey,
            Reason = reason
        };

    private static string NormalizeTopic(string topic)
        => topic.Trim().Replace(" ", "_").ToLowerInvariant();

    private static string FirstNonEmpty(params string[] values)
        => values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? string.Empty;

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
                return text ?? string.Empty;

            if (node is JsonObject version)
                return $"{ReadInt(version, "Major", "major")}.{ReadInt(version, "Minor", "minor")}.{ReadInt(version, "Patch", "patch")}";
        }

        return string.Empty;
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
