namespace Krackend.Sagas.Orchestrations.Runtime.Distribution;

/// <summary>
/// Builds and parses secret references owned by runtime design node storage.
/// </summary>
public sealed class RuntimeDesignNodeSecretReference
{
    /// <summary>
    /// Gets the secret reference prefix used by runtime design nodes.
    /// </summary>
    public const string Prefix = "runtime-design-node:";

    /// <summary>
    /// Builds a secret reference for a design node key.
    /// </summary>
    public string Build(string designNodeKey)
    {
        if (string.IsNullOrWhiteSpace(designNodeKey))
        {
            throw new ArgumentException("Design node key is required.", nameof(designNodeKey));
        }

        return $"{Prefix}{designNodeKey.Trim()}";
    }

    /// <summary>
    /// Tries to read a design node key from a secret reference.
    /// </summary>
    public bool TryReadKey(string secretReference, out string designNodeKey)
    {
        designNodeKey = string.Empty;
        if (string.IsNullOrWhiteSpace(secretReference) ||
            !secretReference.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        designNodeKey = secretReference[Prefix.Length..].Trim();
        return !string.IsNullOrWhiteSpace(designNodeKey);
    }
}
