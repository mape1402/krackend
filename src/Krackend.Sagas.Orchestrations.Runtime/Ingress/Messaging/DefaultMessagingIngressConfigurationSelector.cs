using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Microsoft.Extensions.Logging;

namespace Krackend.Sagas.Orchestrations.Runtime.Ingress.Messaging;

/// <summary>
/// Selects messaging ingress configurations and resolves duplicate trigger subscriptions for the same orchestration.
/// </summary>
public sealed class DefaultMessagingIngressConfigurationSelector : IMessagingIngressConfigurationSelector
{
    private readonly IMessagingConfigurationSerializer _serializer;
    private readonly ILogger<DefaultMessagingIngressConfigurationSelector> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultMessagingIngressConfigurationSelector"/> class.
    /// </summary>
    /// <param name="serializer">Messaging configuration serializer.</param>
    /// <param name="logger">Logger.</param>
    public DefaultMessagingIngressConfigurationSelector(
        IMessagingConfigurationSerializer serializer,
        ILogger<DefaultMessagingIngressConfigurationSelector> logger)
    {
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public IReadOnlyCollection<IngressConfiguration> SelectMatching(
        IReadOnlyCollection<IngressConfiguration> configurations,
        string topic,
        string version)
    {
        if (configurations is null || configurations.Count == 0)
        {
            return [];
        }

        var matches = new List<(IngressConfiguration Configuration, MessagingConfiguration Settings)>();
        foreach (var configuration in configurations)
        {
            if (TryMatch(configuration, topic, version, out var settings))
            {
                matches.Add((configuration, settings));
            }
        }

        if (matches.Count == 0)
        {
            return [];
        }

        return matches
            .GroupBy(
                match => BuildSelectionKey(match.Configuration, match.Settings),
                StringComparer.OrdinalIgnoreCase)
            .Select(group => SelectPreferredConfiguration(group.ToArray(), version))
            .ToArray();
    }

    private bool TryMatch(
        IngressConfiguration configuration,
        string topic,
        string version,
        out MessagingConfiguration settings)
    {
        settings = null;
        if (configuration.IngressTransport != IngressTransport.Messaging)
        {
            return false;
        }

        try
        {
            settings = _serializer.Deserialize(configuration.SettingsPayload);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Messaging ingress configuration '{IngressConfigurationId}' could not be deserialized.",
                configuration.Id);
            return false;
        }

        if (!string.Equals(settings.Topic, topic, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(settings.Version, version, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    private IngressConfiguration SelectPreferredConfiguration(
        IReadOnlyCollection<(IngressConfiguration Configuration, MessagingConfiguration Settings)> matches,
        string incomingVersion)
    {
        if (matches.Count == 1)
        {
            return matches.First().Configuration;
        }

        var exactVersionMatches = matches
            .Where(match => string.Equals(
                match.Configuration.OrchestrationVersion,
                incomingVersion,
                StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var candidates = exactVersionMatches.Length > 0
            ? exactVersionMatches
            : matches.ToArray();
        var selected = candidates
            .OrderByDescending(match => TryParseSemanticVersion(match.Configuration.OrchestrationVersion, out var parsed)
                ? parsed
                : new SemanticVersion(0, 0, 0))
            .ThenByDescending(match => match.Configuration.DeployedOnUtc)
            .First()
            .Configuration;

        _logger.LogWarning(
            "Multiple messaging ingress configurations matched topic '{Topic}' version '{Version}' for orchestration '{OrchestrationDefinitionKey}'. Selected artifact '{SelectedArtifactId}' version '{SelectedVersion}'.",
            matches.First().Settings.Topic,
            matches.First().Settings.Version,
            ResolveOrchestrationSelectionKey(matches.First().Configuration),
            selected.ArtifactId,
            selected.OrchestrationVersion);

        return selected;
    }

    private string BuildSelectionKey(IngressConfiguration configuration, MessagingConfiguration settings)
        => string.Join(
            "|",
            configuration.IngressKind,
            configuration.IngressTransport,
            ResolveOrchestrationSelectionKey(configuration),
            Normalize(settings.Topic),
            Normalize(settings.Version));

    private static string ResolveOrchestrationSelectionKey(IngressConfiguration configuration)
        => string.IsNullOrWhiteSpace(configuration.OrchestrationDefinitionKey)
            ? Normalize(configuration.ArtifactId)
            : Normalize(configuration.OrchestrationDefinitionKey);

    private static string Normalize(string value)
        => (value ?? string.Empty).Trim().ToLowerInvariant();

    private static bool TryParseSemanticVersion(string value, out SemanticVersion version)
    {
        version = default;
        var parts = (value ?? string.Empty).Split('.');
        if (parts.Length != 3 ||
            !int.TryParse(parts[0], out var major) ||
            !int.TryParse(parts[1], out var minor) ||
            !int.TryParse(parts[2], out var patch))
        {
            return false;
        }

        version = new SemanticVersion(major, minor, patch);
        return true;
    }
}
