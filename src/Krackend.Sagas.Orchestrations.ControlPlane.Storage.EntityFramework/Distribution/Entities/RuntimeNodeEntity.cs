using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Distribution.Security;
using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Enums;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Distribution.Entities;

public sealed class RuntimeNodeEntity
{
    public Id Id { get; set; }
    public string Name { get; set; }
    public string Code { get; set; }
    public DistributionMode DistributionMode { get; set; }
    public string EndpointBaseUri { get; set; }
    public string EndpointApiPath { get; set; }
    public RuntimeNodeStatus Status { get; set; }
    public bool IsEnabled { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public string Description { get; set; }
    public int AccessTokenTtlSeconds { get; set; }
    public int TokenRefreshSkewSeconds { get; set; }
    public int TokenValidationCacheTtlSeconds { get; set; }
    public string InboundClientId { get; set; }
    public string InboundKeyId { get; set; }
    public string InboundSecretHash { get; set; }
    public string InboundAllowedScopes { get; set; }
    public ConnectionCredentialStatus InboundCredentialStatus { get; set; }
    public DateTime? InboundCredentialCreatedAtUtc { get; set; }
    public DateTime? InboundCredentialRotatedAtUtc { get; set; }
    public DateTime? InboundCredentialRevokedAtUtc { get; set; }
    public DateTime? InboundLastTokenIssuedAtUtc { get; set; }
    public DateTime? InboundLastTokenFailedAtUtc { get; set; }
    public string InboundLastFailureReason { get; set; }
    public string OutboundClientId { get; set; }
    public string OutboundKeyId { get; set; }
    public string ProtectedOutboundSecret { get; set; }
    public string OutboundRequestedScopes { get; set; }
    public ConnectionCredentialStatus OutboundCredentialStatus { get; set; }
    public DateTime? OutboundCredentialImportedAtUtc { get; set; }
    public DateTime? OutboundLastTokenReceivedAtUtc { get; set; }
    public DateTime RegisteredAtUtc { get; set; }
    public DateTime? LastUpdatedAtUtc { get; set; }
    public ICollection<RuntimeCapabilityEntity> Capabilities { get; set; } = new List<RuntimeCapabilityEntity>();
}

