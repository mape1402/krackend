using Krackend.Sagas.Orchestrations.Client.Abstractions;
using PigeonSemanticVersion = Pigeon.Messaging.Contracts.SemanticVersion;

namespace Krackend.Sagas.Orchestrations.Client.Pigeon;

/// <summary>
/// Converts Krackend orchestration client versions to Pigeon versions.
/// </summary>
public static class OrchestrationSemanticVersionPigeonExtensions
{
    /// <summary>
    /// Converts a Krackend semantic version to Pigeon's semantic version value.
    /// </summary>
    public static PigeonSemanticVersion ToPigeonSemanticVersion(this OrchestrationSemanticVersion version)
        => new(version.Major, version.Minor, version.Patch);
}
