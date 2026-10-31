namespace Krackend.Sagas.Orchestrations.Tests.Runtime;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Runtime.Buffering.Mule;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Recovery;
using Mule;
using NSubstitute;

public sealed class MuleReconciliationInspectorTests
{
    [Fact]
    public async Task UnpersistedReferencedTimerDoesNotFallBackToAnOlderAction()
    {
        var storage = Substitute.For<IMuleStorage>();
        var reader = Substitute.For<IOrchestrationDurableActionStateReader>();
        var inspector = new MuleOrchestrationDurableWorkInspector(storage, reader);
        var task = new TaskExecution { Id = Id.New(), TaskKey = "task", TimeoutActionId = Id.New().ToString(), ResponseDeadlineUtc = DateTime.UtcNow.AddMinutes(1) };
        var attempt = new TaskExecutionAttempt { Id = Id.New(), TaskExecutionId = task.Id, AttemptNumber = 1 };
        reader.ReadAsync(task.TimeoutActionId, Arg.Any<CancellationToken>()).Returns(DurableWorkPresence.Unknown);
        Assert.Equal(DurableWorkPresence.Unknown, await inspector.InspectTimeoutAsync(task, attempt));
        await storage.DidNotReceiveWithAnyArgs().FindByDeduplicationKeyAsync(default, default!);
    }
}
