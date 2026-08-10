using Krackend.Sagas.Orchestrations.Engine;

namespace Krackend.Sagas.Orchestrations.Web;

public interface IRuntimeTriggerInteractionService
{
    Task<RuntimeTriggerResult> Enqueue(
        RuntimeTriggerRequest request,
        CancellationToken cancellationToken = default);

    Task<RuntimeEngineProcessResult> ProcessNext(CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<RuntimeEngineProcessResult>> ProcessAll(
        int maxItems,
        CancellationToken cancellationToken = default);
}
