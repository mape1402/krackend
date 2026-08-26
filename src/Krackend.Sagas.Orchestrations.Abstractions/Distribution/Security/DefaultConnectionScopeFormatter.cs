namespace Krackend.Sagas.Orchestrations.Abstractions.Distribution.Security;

/// <summary>
/// Default artifact delivery scope formatter.
/// </summary>
public sealed class DefaultConnectionScopeFormatter : IConnectionScopeFormatter
{
    /// <inheritdoc />
    public string Format(ArtifactDeliveryScope scope)
        => scope switch
        {
            ArtifactDeliveryScope.ArtifactPush => "artifact:push",
            ArtifactDeliveryScope.ReleaseRead => "release:read",
            ArtifactDeliveryScope.ArtifactRead => "artifact:read",
            ArtifactDeliveryScope.ArtifactAcknowledge => "artifact:ack",
            ArtifactDeliveryScope.ConnectionValidate => "connection:validate",
            _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, "Unsupported artifact delivery scope.")
        };

    /// <inheritdoc />
    public string FormatMany(IEnumerable<ArtifactDeliveryScope> scopes)
        => string.Join(' ', (scopes ?? Array.Empty<ArtifactDeliveryScope>()).Select(Format).Distinct(StringComparer.Ordinal));

    /// <inheritdoc />
    public IReadOnlyCollection<ArtifactDeliveryScope> ParseMany(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Array.Empty<ArtifactDeliveryScope>();
        }

        return value
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(Parse)
            .Distinct()
            .ToArray();
    }

    private static ArtifactDeliveryScope Parse(string value)
        => value switch
        {
            "artifact:push" => ArtifactDeliveryScope.ArtifactPush,
            "release:read" => ArtifactDeliveryScope.ReleaseRead,
            "artifact:read" => ArtifactDeliveryScope.ArtifactRead,
            "artifact:ack" => ArtifactDeliveryScope.ArtifactAcknowledge,
            "connection:validate" => ArtifactDeliveryScope.ConnectionValidate,
            _ => throw new InvalidOperationException($"Artifact delivery scope '{value}' is not supported.")
        };
}
