using Krackend.Sagas.Orchestrations.Engine;

namespace Krackend.Sagas.Orchestrations.Web;

public interface IRuntimeTriggerInteractionService
{
    Task<RuntimeTriggerResult> Enqueue(
        RuntimeTriggerRequest request,
        CancellationToken cancellationToken = default);
}
