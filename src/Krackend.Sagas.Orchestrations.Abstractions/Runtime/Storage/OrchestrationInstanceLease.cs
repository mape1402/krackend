using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

namespace Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;

/// <summary>
/// Represents an exclusive storage lease for mutating one orchestration instance.
/// </summary>
public sealed record OrchestrationInstanceLease(
    Id InstanceId,
    string LeaseId,
    DateTime AcquiredOnUtc,
    DateTime ExpiresOnUtc);
