using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Computes deterministic hashes for metadata descriptor schemas.
/// </summary>
internal static class MetadataDescriptorSchemaHasher
{
    /// <summary>
    /// Normalizes a descriptor JSON schema.
    /// </summary>
    /// <param name="schemaJson">Schema JSON.</param>
    /// <returns>Compact schema JSON.</returns>
    public static string Normalize(string schemaJson)
    {
        using var document = JsonDocument.Parse(schemaJson);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("Metadata descriptor schema must be a JSON object.");
        }

        return document.RootElement.GetRawText();
    }

    /// <summary>
    /// Computes a SHA-256 hash for a descriptor schema.
    /// </summary>
    /// <param name="schemaJson">Schema JSON.</param>
    /// <returns>Lowercase hex content hash.</returns>
    public static string ComputeHash(string schemaJson)
    {
        var normalized = Normalize(schemaJson);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    /// <summary>
    /// Determines whether the input is a valid JSON object.
    /// </summary>
    /// <param name="schemaJson">Schema JSON.</param>
    /// <returns>True when the input is a JSON object.</returns>
    public static bool IsJsonObject(string schemaJson)
    {
        if (string.IsNullOrWhiteSpace(schemaJson))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(schemaJson);
            return document.RootElement.ValueKind == JsonValueKind.Object;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
