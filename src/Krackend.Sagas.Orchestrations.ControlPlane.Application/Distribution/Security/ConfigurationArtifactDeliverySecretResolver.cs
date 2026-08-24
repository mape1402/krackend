using Krackend.Sagas.Orchestrations.Abstractions.Distribution.Security;
using Microsoft.Extensions.Configuration;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Distribution;

/// <summary>
/// Resolves artifact delivery secrets from application configuration.
/// </summary>
public sealed class ConfigurationArtifactDeliverySecretResolver : IArtifactDeliverySecretResolver
{
    private readonly IConfiguration _configuration;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigurationArtifactDeliverySecretResolver"/> class.
    /// </summary>
    public ConfigurationArtifactDeliverySecretResolver(IConfiguration configuration)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    /// <inheritdoc />
    public Task<string> ResolveSecret(string secretReference, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(secretReference))
        {
            throw new InvalidOperationException("Artifact delivery secret reference is required.");
        }

        var trimmed = secretReference.Trim();
        var configured = _configuration[trimmed]
            ?? _configuration[$"ArtifactDelivery:Secrets:{trimmed}"];

        if (string.IsNullOrWhiteSpace(configured))
        {
            throw new InvalidOperationException($"Artifact delivery secret reference '{trimmed}' was not found.");
        }

        return Task.FromResult(configured);
    }
}
