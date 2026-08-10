namespace Krackend.Sagas.Orchestrations.Distribution.Core;

public sealed class OrchestrationProjection
{
    public string Id { get; set; }
    public string Key { get; set; }
    public string Name { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
}


