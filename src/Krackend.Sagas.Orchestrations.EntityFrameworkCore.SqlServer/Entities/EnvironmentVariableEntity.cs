using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

namespace Krackend.Sagas.Orchestrations.EntityFrameworkCore.SqlServer.Entities;

public sealed class EnvironmentVariableEntity
{
    public Id Id { get; set; }
    public string EnvironmentKey { get; set; }
    public string VariableKey { get; set; }
    public VariableValueType ValueType { get; set; }
    public string ValueJson { get; set; }
    public bool IsSensitive { get; set; }
    public bool IsResolved { get; set; }
    public DateTime LastValidatedOnUtc { get; set; }
    public DateTime CreatedOnUtc { get; set; }
    public DateTime UpdatedOnUtc { get; set; }
    public string UpdatedBy { get; set; }
    public string Notes { get; set; }
}
