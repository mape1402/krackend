namespace Krackend.Sagas.Orchestrations.Runtime.Api;

/// <summary>
/// Represents a runtime ingress configuration returned by the REST API.
/// </summary>
public sealed record RuntimeIngressConfigurationApiModel(
    string Id,
    string RuntimeOrchestrationArtifactId,
    string ConfigurationKey,
    string IngressKind,
    string IngressTransport,
    string SettingsPayload,
    bool IsActive,
    DateTime CreatedOnUtc,
    DateTime UpdatedOnUtc,
    DateTime? DeactivatedOnUtc);
