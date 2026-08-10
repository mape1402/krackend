namespace Krackend.Sagas.Orchestrations.Distribution.Storage.SqlServer.Entities;

public sealed class OrchestrationProjectionEntity
{
    public string Id { get; set; }
    public string Key { get; set; }
    public string Name { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }

    public ICollection<OrchestrationAllowedRuntimeNodeEntity> AllowedRuntimeNodes { get; set; } = new List<OrchestrationAllowedRuntimeNodeEntity>();
}


