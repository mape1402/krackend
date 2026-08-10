using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

namespace Krackend.Sagas.Orchestrations.Distribution.Core;

public sealed class ReleaseAttempt
{
    public Id Id { get; set; }
    public Id ReleaseTargetId { get; set; }
    public string Action { get; set; }
    public string InitiatedBy { get; set; }
    public DateTime StartedAtUtc { get; set; }
    public DateTime? FinishedAtUtc { get; set; }
    public bool Succeeded { get; set; }
    public string ErrorCode { get; set; }
    public string ErrorMessage { get; set; }
    public string ExternalReference { get; set; }
}

