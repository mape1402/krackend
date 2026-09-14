using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Enums;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Distribution;

/// <summary>
/// Represents a runtime node row exposed by the distribution application layer.
/// </summary>
public sealed class RuntimeNodeModel
{
    /// <summary>
    /// Gets or sets the runtime node id.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the display name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the runtime-local node code.
    /// </summary>
    public string Code { get; set; }

    /// <summary>
    /// Gets or sets the distribution mode label.
    /// </summary>
    public string DistributionMode { get; set; }

    /// <summary>
    /// Gets or sets the runtime endpoint base URI.
    /// </summary>
    public string EndpointBaseUri { get; set; }

    /// <summary>
    /// Gets or sets the runtime artifact delivery endpoint path.
    /// </summary>
    public string EndpointApiPath { get; set; }

    /// <summary>
    /// Gets or sets the runtime node status label.
    /// </summary>
    public string Status { get; set; }

    /// <summary>
    /// Gets or sets the inbound credential status label.
    /// </summary>
    public string InboundCredentialStatus { get; set; }

    /// <summary>
    /// Gets or sets the outbound credential status label.
    /// </summary>
    public string OutboundCredentialStatus { get; set; }

    /// <summary>
    /// Gets or sets token lifetime in seconds for Design-issued tokens.
    /// </summary>
    public int AccessTokenTtlSeconds { get; set; }

    /// <summary>
    /// Gets or sets how early outbound tokens should be refreshed.
    /// </summary>
    public int TokenRefreshSkewSeconds { get; set; }

    /// <summary>
    /// Gets or sets how long positive token validations are cached.
    /// </summary>
    public int TokenValidationCacheTtlSeconds { get; set; }

    /// <summary>
    /// Gets or sets the inbound client id that Runtime uses to request Design tokens.
    /// </summary>
    public string InboundClientId { get; set; }

    /// <summary>
    /// Gets or sets the inbound credential key id.
    /// </summary>
    public string InboundKeyId { get; set; }

    /// <summary>
    /// Gets or sets space-separated scopes allowed for inbound Runtime calls.
    /// </summary>
    public string InboundAllowedScopes { get; set; }

    /// <summary>
    /// Gets or sets when the inbound credential was created.
    /// </summary>
    public DateTime? InboundCredentialCreatedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets when the inbound credential was rotated.
    /// </summary>
    public DateTime? InboundCredentialRotatedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets when the inbound credential was revoked.
    /// </summary>
    public DateTime? InboundCredentialRevokedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets when the last inbound token was issued.
    /// </summary>
    public DateTime? InboundLastTokenIssuedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets when the last inbound token request failed.
    /// </summary>
    public DateTime? InboundLastTokenFailedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets the last inbound token failure reason.
    /// </summary>
    public string InboundLastFailureReason { get; set; }

    /// <summary>
    /// Gets or sets the outbound client id imported from Runtime.
    /// </summary>
    public string OutboundClientId { get; set; }

    /// <summary>
    /// Gets or sets the outbound credential key id imported from Runtime.
    /// </summary>
    public string OutboundKeyId { get; set; }

    /// <summary>
    /// Gets or sets space-separated scopes requested by Design when calling Runtime.
    /// </summary>
    public string OutboundRequestedScopes { get; set; }

    /// <summary>
    /// Gets or sets when the outbound credential was imported.
    /// </summary>
    public DateTime? OutboundCredentialImportedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets when the last outbound token was received.
    /// </summary>
    public DateTime? OutboundLastTokenReceivedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the node is enabled.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the node was removed from active configuration.
    /// </summary>
    public bool IsDeleted { get; set; }

    /// <summary>
    /// Gets or sets when the node was removed from active configuration.
    /// </summary>
    public DateTime? DeletedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets the node description.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets when the node was registered.
    /// </summary>
    public DateTime RegisteredAtUtc { get; set; }

    /// <summary>
    /// Gets or sets when the node was last updated.
    /// </summary>
    public DateTime? LastUpdatedAtUtc { get; set; }
}

/// <summary>
/// Captures values required to create or update a runtime node.
/// </summary>
public sealed record UpsertRuntimeNodeInput(
    /// <summary>
    /// Runtime node id when updating an existing node.
    /// </summary>
    string RuntimeNodeId,
    /// <summary>
    /// Runtime node display name.
    /// </summary>
    string Name,
    /// <summary>
    /// Runtime node code.
    /// </summary>
    string Code,
    /// <summary>
    /// Distribution mode.
    /// </summary>
    DistributionMode DistributionMode,
    /// <summary>
    /// Runtime endpoint base URI.
    /// </summary>
    string EndpointBaseUri,
    /// <summary>
    /// Runtime node description.
    /// </summary>
    string Description);
