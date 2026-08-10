using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

namespace Krackend.Sagas.Orchestrations.EntityFrameworkCore.SqlServer.Entities;

public sealed class InstanceVariableEntity
{
    public Id Id { get; set; }
    public Id OrchestrationInstanceId { get; set; }
    public string Key { get; set; }
    public VariableScope Scope { get; set; }
    public VariableValueType ValueType { get; set; }
    public string ValueJson { get; set; }
    public bool IsSensitive { get; set; }
    public string SourceType { get; set; }
    public string SourceReference { get; set; }
    public DateTime CreatedOnUtc { get; set; }
    public DateTime? UpdatedOnUtc { get; set; }
    public string LastUpdatedBy { get; set; }
}
