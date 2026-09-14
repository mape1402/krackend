using System.Text;
using System.Text.Json;

namespace Krackend.Sagas.Orchestrations.Abstractions.Distribution.Security;

/// <summary>
/// Serializes and parses portable Design/Runtime connection credential packages.
/// </summary>
public sealed class ConnectionCredentialPackageSerializer
{
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    /// <summary>
    /// Creates a package envelope containing readable JSON and Base64 JSON.
    /// </summary>
    /// <param name="package">Credential package to serialize.</param>
    /// <returns>Serialized package envelope.</returns>
    public ConnectionCredentialPackageEnvelope CreateEnvelope(ConnectionCredentialPackage package)
    {
        ArgumentNullException.ThrowIfNull(package);

        var json = JsonSerializer.Serialize(package, _jsonOptions);
        return new ConnectionCredentialPackageEnvelope
        {
            Json = json,
            Base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(json)),
            Package = package
        };
    }

    /// <summary>
    /// Parses JSON or Base64 JSON into a credential package.
    /// </summary>
    /// <param name="value">JSON or Base64 JSON value.</param>
    /// <returns>Parsed credential package.</returns>
    public ConnectionCredentialPackage Parse(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException("Credential package is required.");
        }

        var trimmed = value.Trim();
        var json = trimmed.StartsWith('{')
            ? trimmed
            : Encoding.UTF8.GetString(Convert.FromBase64String(trimmed));

        return JsonSerializer.Deserialize<ConnectionCredentialPackage>(json, _jsonOptions)
            ?? throw new InvalidOperationException("Credential package is empty.");
    }
}
