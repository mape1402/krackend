using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Enums;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Core;

public sealed class RuntimeNode
{
    public Id Id { get; set; }
    public string Name { get; set; }
    public string Code { get; set; }
    public Id EnvironmentId { get; set; }
    public string EnvironmentName { get; set; }
    public DistributionMode DistributionMode { get; set; }
    public string EndpointBaseUri { get; set; }
    public string EndpointApiPath { get; set; }
    public RuntimeAuthenticationMode AuthenticationMode { get; set; }
    public string ClientId { get; set; }
    public string SecretReference { get; set; }
    public string ApiKeyReference { get; set; }
    public RuntimeNodeStatus Status { get; set; }
    public bool IsEnabled { get; set; }
    public string Description { get; set; }
    public DateTime RegisteredAtUtc { get; set; }
    public DateTime? LastUpdatedAtUtc { get; set; }
    public List<RuntimeCapability> Capabilities { get; set; } = new();
}
