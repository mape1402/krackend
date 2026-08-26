using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Distribution.Security;
using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Enums;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Core;

/// <summary>
/// Represents a Runtime node registered in the Control Plane for artifact distribution.
/// </summary>
public sealed class RuntimeNode
{
    /// <summary>
    /// Gets or sets the runtime node id.
    /// </summary>
    public Id Id { get; set; }

    /// <summary>
    /// Gets or sets the display name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the control-plane-local code.
    /// </summary>
    public string Code { get; set; }

    /// <summary>
    /// Gets or sets the runtime environment id.
    /// </summary>
    public Id EnvironmentId { get; set; }

    /// <summary>
    /// Gets or sets the runtime environment display name.
    /// </summary>
    public string EnvironmentName { get; set; }

    /// <summary>
    /// Gets or sets who can initiate artifact distribution.
    /// </summary>
    public DistributionMode DistributionMode { get; set; }

    /// <summary>
    /// Gets or sets the runtime endpoint base URI.
    /// </summary>
    public string EndpointBaseUri { get; set; }

    /// <summary>
    /// Gets or sets the runtime artifact delivery endpoint path.
    /// </summary>
    public string EndpointApiPath { get; set; }

    /// <summary>
    /// Gets or sets the runtime node operational status.
    /// </summary>
    public RuntimeNodeStatus Status { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this runtime node can be selected for new release distribution.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this runtime node was removed from active configuration.
    /// </summary>
    public bool IsDeleted { get; set; }

    /// <summary>
    /// Gets or sets when this runtime node was removed from active configuration.
    /// </summary>
    public DateTime? DeletedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets an optional description.
    /// </summary>
    public string Description { get; set; }

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
    /// Gets or sets the inbound client secret hash.
    /// </summary>
    public string InboundSecretHash { get; set; }

    /// <summary>
    /// Gets or sets space-separated scopes allowed for inbound Runtime calls.
    /// </summary>
    public string InboundAllowedScopes { get; set; }

    /// <summary>
    /// Gets or sets the inbound credential status.
    /// </summary>
    public ConnectionCredentialStatus InboundCredentialStatus { get; set; }

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
    /// Gets or sets the protected outbound secret imported from Runtime.
    /// </summary>
    public string ProtectedOutboundSecret { get; set; }

    /// <summary>
    /// Gets or sets space-separated scopes requested by Design when calling Runtime.
    /// </summary>
    public string OutboundRequestedScopes { get; set; }

    /// <summary>
    /// Gets or sets the outbound credential status.
    /// </summary>
    public ConnectionCredentialStatus OutboundCredentialStatus { get; set; }

    /// <summary>
    /// Gets or sets when the outbound credential was imported.
    /// </summary>
    public DateTime? OutboundCredentialImportedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets when the last outbound token was received.
    /// </summary>
    public DateTime? OutboundLastTokenReceivedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets when the runtime node was registered.
    /// </summary>
    public DateTime RegisteredAtUtc { get; set; }

    /// <summary>
    /// Gets or sets when the runtime node was last updated.
    /// </summary>
    public DateTime? LastUpdatedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets runtime capabilities declared for this node.
    /// </summary>
    public List<RuntimeCapability> Capabilities { get; set; } = new();
}
