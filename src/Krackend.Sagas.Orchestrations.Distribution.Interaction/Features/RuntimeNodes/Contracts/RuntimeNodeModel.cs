using Krackend.Sagas.Orchestrations.Distribution.Enums;

namespace Krackend.Sagas.Orchestrations.Distribution.Interaction;

public sealed class RuntimeNodeModel
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Code { get; set; }
    public string EnvironmentId { get; set; }
    public string EnvironmentName { get; set; }
    public string DistributionMode { get; set; }
    public string EndpointBaseUri { get; set; }
    public string Status { get; set; }
    public bool IsEnabled { get; set; }
    public string Description { get; set; }
    public DateTime RegisteredAtUtc { get; set; }
}

public sealed record UpsertRuntimeNodeInput(
    string RuntimeNodeId,
    string Name,
    string Code,
    string EnvironmentId,
    DistributionMode DistributionMode,
    string EndpointBaseUri,
    string Description);
