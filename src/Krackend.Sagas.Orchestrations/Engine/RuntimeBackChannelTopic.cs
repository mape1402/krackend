namespace Krackend.Sagas.Orchestrations.Engine;

/// <summary>
/// Builds the runtime back-channel topic used by external services to answer orchestration tasks.
/// </summary>
public static class RuntimeBackChannelTopic
{
    /// <summary>
    /// Builds a normalized topic using the orchestration key.
    /// </summary>
    /// <param name="orchestrationKey">Orchestration key from the deployed artifact.</param>
    /// <returns>Back-channel topic.</returns>
    public static string Build(string orchestrationKey)
    {
        if (string.IsNullOrWhiteSpace(orchestrationKey))
            return "orchestrations.unknown";

        var normalized = orchestrationKey.Trim().Replace("::", ".").Replace(" ", "_");
        return normalized.StartsWith("orchestrations.", StringComparison.OrdinalIgnoreCase)
            ? normalized.ToLowerInvariant()
            : $"orchestrations.{normalized}".ToLowerInvariant();
    }
}
