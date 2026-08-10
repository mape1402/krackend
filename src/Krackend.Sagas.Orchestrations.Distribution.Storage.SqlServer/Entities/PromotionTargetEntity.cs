using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Distribution.Enums;

namespace Krackend.Sagas.Orchestrations.Distribution.Storage.SqlServer.Entities;

public sealed class ReleasePlanTargetEntity
{
    public Id Id { get; set; }
    public Id ReleaseId { get; set; }
    public Id RuntimeNodeId { get; set; }
    public ReleaseStatus Status { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public string Notes { get; set; }
    public ReleaseEntity Release { get; set; }
    public RuntimeNodeEntity RuntimeNode { get; set; }
}

