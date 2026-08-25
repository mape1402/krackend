using Krackend.Sagas.Orchestrations.Abstractions.Distribution.Security;
using Microsoft.Extensions.Configuration;

namespace Krackend.Sagas.Orchestrations.Runtime.Distribution;

/// <summary>
/// Resolves artifact delivery secrets from runtime design node storage or configuration.
/// </summary>
public sealed class RuntimeDesignNodeSecretResolver : IArtifactDeliverySecretResolver
{
    private readonly IConfiguration _configuration;
    private readonly IRuntimeDesignNodeRepository _repository;
    private readonly IRuntimeDesignNodeSecretProtector _protector;
    private readonly RuntimeDesignNodeSecretReference _secretReference;

    /// <summary>
    /// Initializes a new instance of the <see cref="RuntimeDesignNodeSecretResolver"/> class.
    /// </summary>
    public RuntimeDesignNodeSecretResolver(
        IConfiguration configuration,
        IRuntimeDesignNodeRepository repository,
        IRuntimeDesignNodeSecretProtector protector,
        RuntimeDesignNodeSecretReference secretReference)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _protector = protector ?? throw new ArgumentNullException(nameof(protector));
        _secretReference = secretReference ?? throw new ArgumentNullException(nameof(secretReference));
    }

    /// <inheritdoc />
    public async Task<string> ResolveSecret(string secretReference, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(secretReference))
        {
            throw new InvalidOperationException("Artifact delivery secret reference is required.");
        }

        if (_secretReference.TryReadKey(secretReference, out var designNodeKey))
        {
            var designNode = await _repository.GetByKeyAsync(designNodeKey, cancellationToken);
            if (designNode is null || string.IsNullOrWhiteSpace(designNode.ProtectedSecret))
            {
                throw new InvalidOperationException($"Runtime design node secret '{designNodeKey}' was not found.");
            }

            return _protector.Unprotect(designNode.ProtectedSecret);
        }

        var trimmed = secretReference.Trim();
        var secret = _configuration[trimmed]
            ?? _configuration[$"ArtifactDelivery:Secrets:{trimmed}"];

        if (string.IsNullOrWhiteSpace(secret))
        {
            throw new InvalidOperationException($"Artifact delivery secret reference '{trimmed}' was not found.");
        }

        return secret;
    }
}
