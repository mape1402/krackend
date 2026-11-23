// Minimal test dependencies; production coordinator and publisher are linked unchanged.
namespace Krackend.Sagas.Orchestrations.Abstractions.Primitives
{
    public readonly record struct Id(Guid Value)
    {
        public static Id New() => new(Guid.NewGuid());
        public override string ToString() => Value.ToString();
    }
}
namespace Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage
{
    using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
    public interface IOrchestrationInstanceRepository
    {
        Task<OrchestrationInstanceLease> TryAcquireLease(Id id, string leaseId, DateTime now, DateTime expires, CancellationToken cancellationToken = default);
        Task ReleaseLease(Id id, string leaseId, CancellationToken cancellationToken = default);
    }
}
