namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Distribution;

public sealed class OrchestrationPolicyDefinitionModel
{
    public string Id { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}
