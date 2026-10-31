namespace Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Repositories;

using Krackend.Sagas.Orchestrations.Runtime.Engine.Recovery;
using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Mule;

internal sealed class OrchestrationDurableActionStateReader(RuntimeDbContext db) : IOrchestrationDurableActionStateReader
{
    public async Task<DurableWorkPresence> ReadAsync(string actionId, CancellationToken cancellationToken = default)
    {
        if (!global::Mule.Ulid.TryParse(actionId, out var id)) return DurableWorkPresence.Unknown;
        var status = await db.Set<DurableAction>().AsNoTracking().Where(action => action.Id == id)
            .Select(action => (DurableActionStatus?)action.Status).FirstOrDefaultAsync(cancellationToken);
        return status switch
        {
            DurableActionStatus.Pending or DurableActionStatus.Locked => DurableWorkPresence.Present,
            DurableActionStatus.Completed or DurableActionStatus.Failed or DurableActionStatus.Cancelled => DurableWorkPresence.Terminal,
            _ => DurableWorkPresence.Unknown
        };
    }
}
