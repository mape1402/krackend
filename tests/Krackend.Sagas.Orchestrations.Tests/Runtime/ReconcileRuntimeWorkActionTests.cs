using Krackend.Sagas.Orchestrations.Engine;
using Krackend.Sagas.Orchestrations.Engine.DurableWork;
using Microsoft.Extensions.DependencyInjection;
using Mule;

namespace Krackend.Sagas.Orchestrations.Tests.Runtime;

public sealed class ReconcileRuntimeWorkActionTests
{
    [Fact]
    public async Task ExecuteAsync_Should_Process_Due_Work()
    {
        var processor = new CapturingPendingWorkProcessor();
        var action = new ReconcileRuntimeWorkAction(processor);
        var request = new RuntimeReconcileRequest
        {
            ReconcileKey = "runtime-reconcile:123",
            DueOnUtc = new DateTime(2026, 8, 14, 1, 2, 3, DateTimeKind.Utc)
        };

        await action.ExecuteAsync(CreateContext(request), CancellationToken.None);

        Assert.Equal(request.DueOnUtc, processor.DueOnUtc);
    }

    private static MuleActionContext<RuntimeReconcileRequest> CreateContext(RuntimeReconcileRequest request)
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var action = new DurableAction
        {
            Id = Guid.NewGuid(),
            Key = RuntimeDurableWorkActionKeys.Reconcile,
            Payload = "{}",
            PayloadType = typeof(RuntimeReconcileRequest).AssemblyQualifiedName,
            Status = DurableActionStatus.Locked,
            CreatedOnUtc = DateTimeOffset.UtcNow
        };

        return new MuleActionContext<RuntimeReconcileRequest>(action, services, request);
    }

    private sealed class CapturingPendingWorkProcessor : IRuntimePendingWorkProcessor
    {
        public DateTime DueOnUtc { get; private set; }

        public Task<RuntimePendingWorkResult> ProcessDueWork(DateTime nowUtc, CancellationToken cancellationToken = default)
        {
            DueOnUtc = nowUtc;
            return Task.FromResult(new RuntimePendingWorkResult(nowUtc, []));
        }
    }
}
