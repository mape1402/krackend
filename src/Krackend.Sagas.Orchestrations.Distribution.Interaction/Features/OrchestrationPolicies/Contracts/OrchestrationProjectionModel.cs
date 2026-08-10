namespace Krackend.Sagas.Orchestrations.Distribution.Interaction;

public sealed class OrchestrationProjectionModel
{
    public string Id { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}
