namespace Krackend.Sagas.Orchestrations.Engine;

/// <summary>
/// Builds the runtime back-channel topic used by external services to answer orchestration tasks.
/// </summary>
internal static class RuntimeBackChannelTopic
{
    /// <summary>
    /// Builds a normalized topic using the orchestration key.
    /// </summary>
    /// <param name="orchestrationKey">Orchestration key from the deployed artifact.</param>
    /// <returns>Back-channel topic.</returns>
    public static string Build(string orchestrationKey)
        => Build(orchestrationKey, string.Empty);

    /// <summary>
    /// Builds a normalized, version-scoped topic using the orchestration key.
    /// </summary>
    /// <param name="orchestrationKey">Orchestration key from the deployed artifact.</param>
    /// <param name="version">Artifact semantic version.</param>
    /// <returns>Back-channel topic.</returns>
    public static string Build(string orchestrationKey, string version)
    {
        if (string.IsNullOrWhiteSpace(orchestrationKey))
            return AppendVersion("orchestrations.unknown", version);

        var normalized = orchestrationKey.Trim().Replace("::", ".").Replace(" ", "_");
        var topic = normalized.StartsWith("orchestrations.", StringComparison.OrdinalIgnoreCase)
            ? normalized.ToLowerInvariant()
            : $"orchestrations.{normalized}".ToLowerInvariant();

        return AppendVersion(topic, version);
    }

    private static string AppendVersion(string topic, string version)
    {
        if (string.IsNullOrWhiteSpace(version))
            return topic;

        var suffix = $".v{version.Trim().Replace(".", "-")}".ToLowerInvariant();
        return topic.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
            ? topic
            : $"{topic}{suffix}";
    }
}
