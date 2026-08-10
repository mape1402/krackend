using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Distribution.Enums;

namespace Krackend.Sagas.Orchestrations.Distribution.Core;

public sealed class ReleasePlanTarget
{
    public Id Id { get; set; }
    public Id ReleaseId { get; set; }
    public Id RuntimeNodeId { get; set; }
    public ReleaseStatus Status { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public string Notes { get; set; }
}
